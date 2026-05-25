using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
#pragma warning disable 0169

    public static class ProceduralGeologyConstants
    {
        public const int TelemetryFrames = 300;
        public const int MockTerrainResolution = 32;
        public const int DistributionRuleCapacity = 32;
        public const int TuningCapacity = 1;
        public const int CsvScratchBytes = 32768;
        public const int SelfAuditCapacity = 1;
        public const int HzbTileCapacity = 4096;
        public const int HzbMetaCapacity = 1;
        public const int MaxVisualClusterNodesPerCore = 5;
        public const uint VisualOnlyTypeFlag = 1u << 31;
        public const uint HzbActiveFlag = 1u;
        public const uint HzbCullAuthoritativeFlag = 1u << 1;
        public const uint TelemetryFlagHzbCulled = 1u << 8;
        public const uint ResourceTypeMask = 0x7FFFFFFFu;
        public const uint DefaultWorldSeed = 0x48454338u;
        public const uint DumpMagic = 0x47454F38u; // GEO8
        public const uint DumpVersion = 1u;
    }

    public static class ProceduralGeologyVaultBufferIds
    {
        public const BufferID ResourceNodes = (BufferID)71530;
        public const BufferID OrePositions = (BufferID)71531;
        public const BufferID OreTypes = (BufferID)71532;
        public const BufferID DepletionMasks = (BufferID)71533;
        public const BufferID ResourceMatrices = (BufferID)71534;
        public const BufferID BiomeHeatmap = (BufferID)71535;
        public const BufferID SpawnCounts = (BufferID)71536;
        public const BufferID TelemetryRing = (BufferID)71537;
        public const BufferID MockTerrainSdf = (BufferID)71538;
        public const BufferID DistributionRules = (BufferID)71539;
        public const BufferID Tuning = (BufferID)71540;
        public const BufferID CsvScratch = (BufferID)71541;
        public const BufferID SelfAudit = (BufferID)71542;
        public const BufferID CandidateSlots = (BufferID)71543;
        public const BufferID DepletionCacheKeys = (BufferID)71544;
        public const BufferID DepletionCacheMasks = (BufferID)71545;
        public const BufferID DepletionCacheCount = (BufferID)71546;
        public const BufferID SectorHashGrid = (BufferID)71547;
        public const BufferID IndirectArgs = (BufferID)71548;
        public const BufferID HzbTiles = (BufferID)71549;
        public const BufferID HzbMeta = (BufferID)71550;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct ResourceNodeDTO
    {
        [FieldOffset(0)] public float4x4 LocalMatrix;
        [FieldOffset(64)] public uint ResourceTypeHash;
        [FieldOffset(68)] public float YieldRemaining;
        [FieldOffset(72)] public double3 SectorAUP;
        [FieldOffset(96)] private ulong _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GeologyTerrainSampleDTO
    {
        [FieldOffset(0)] public float Height;
        [FieldOffset(4)] public float3 Normal;
        [FieldOffset(16)] public double AbsoluteX;
        [FieldOffset(24)] public double AbsoluteZ;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GeologyDistributionRuleDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint ResourceTypeHash;
        [FieldOffset(8)] public int Weight;
        [FieldOffset(12)] public float MinDepth;
        [FieldOffset(16)] public float MaxDepth;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint RuleHash;
        [FieldOffset(28)] private uint _pad0;
        [FieldOffset(32)] private ulong _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GeologyTuningDTO
    {
        [FieldOffset(0)] public float BaseNodeDensity;
        [FieldOffset(4)] public float ClusterSpreadRadius;
        [FieldOffset(8)] public float SurfaceNormalAlignmentTolerance;
        [FieldOffset(12)] public float VisualClusterDensity;
        [FieldOffset(16)] public float SectorSizeMeters;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;

        public static GeologyTuningDTO Default(float sectorSizeMeters)
        {
            GeologyTuningDTO tuning = default;
            tuning.BaseNodeDensity = 1f;
            tuning.ClusterSpreadRadius = 0.85f;
            tuning.SurfaceNormalAlignmentTolerance = 0.5f;
            tuning.VisualClusterDensity = 1f;
            tuning.SectorSizeMeters = math.max(16f, sectorSizeMeters);
            tuning.GlobalQualityWeight = 1f;
            tuning.Version = 1u;
            return tuning;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GeologyGenerationTelemetryEntry
    {
        [FieldOffset(0)] public long SectorHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public int AuthoritativeNodeCount;
        [FieldOffset(16)] public int RenderNodeCount;
        [FieldOffset(20)] public int DepletedCullCount;
        [FieldOffset(24)] public int VisualOnlyNodeCount;
        [FieldOffset(28)] public int OverflowCount;
        [FieldOffset(32)] public float GenerationBudgetUs;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint FirstNodeHash;
        [FieldOffset(48)] public uint LayoutHash;
        [FieldOffset(52)] public uint ActiveDepletionWord0;
        [FieldOffset(56)] public ulong StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GeologySelfAuditResultDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint ResourceNodeSize;
        [FieldOffset(12)] public uint TelemetrySize;
        [FieldOffset(16)] public uint DeterminismHashA;
        [FieldOffset(20)] public uint DeterminismHashB;
        [FieldOffset(24)] public uint AliasFaults;
        [FieldOffset(28)] public uint ManagedAllocationFaults;
        [FieldOffset(32)] public ulong BufferMaskLow;
        [FieldOffset(40)] public ulong BufferMaskHigh;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct GeologyIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct GeologyHzbTileDTO
    {
        [FieldOffset(0)] public float Depth01;
        [FieldOffset(4)] public uint TileX;
        [FieldOffset(8)] public uint TileY;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct GeologyHzbMetaDTO
    {
        [FieldOffset(0)] public float4x4 CameraRelativeViewProjection;
        [FieldOffset(64)] public int Width;
        [FieldOffset(68)] public int Height;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public float DepthBias;
        [FieldOffset(80)] public float RadiusBiasScale;
        [FieldOffset(84)] public float GlobalQualityWeight;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] private uint _pad0;
        [FieldOffset(96)] private ulong _pad1;
        [FieldOffset(104)] private ulong _pad2;
        [FieldOffset(112)] private ulong _pad3;
        [FieldOffset(120)] private ulong _pad4;
    }

    public static class ProceduralGeologyLayoutAudit
    {
        public const uint LayoutHash = 0x53483135u; // SH15

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<ResourceNodeDTO>() == 128 &&
                   UnsafeUtility.SizeOf<GeologyTerrainSampleDTO>() == 32 &&
                   UnsafeUtility.SizeOf<GeologyDistributionRuleDTO>() == 64 &&
                   UnsafeUtility.SizeOf<GeologyTuningDTO>() == 64 &&
                   UnsafeUtility.SizeOf<GeologyGenerationTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<GeologySelfAuditResultDTO>() == 64 &&
                   UnsafeUtility.SizeOf<GeologyIndirectArgsDTO>() == 16 &&
                   UnsafeUtility.SizeOf<GeologyHzbTileDTO>() == 16 &&
                   UnsafeUtility.SizeOf<GeologyHzbMetaDTO>() == 128;
        }
    }

    public static class ProceduralGeologyHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Next(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix64To32(ulong value)
        {
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            value *= 0xc4ceb9fe1a85ec53UL;
            value ^= value >> 33;
            return unchecked((uint)value ^ (uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockTerrainSDFJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<GeologyTerrainSampleDTO> Samples;
        public int Resolution;
        public double2 SectorOrigin;
        public double SectorSize;
        public float BaseHeight;
        public uint Seed;

        public void Execute(int index)
        {
            int resolution = math.max(2, Resolution);
            if ((uint)index >= (uint)Samples.Length || index >= resolution * resolution)
                return;

            int xIndex = index % resolution;
            int zIndex = index / resolution;
            double inv = math.rcp((double)(resolution - 1));
            double x = SectorOrigin.x + (xIndex * inv * SectorSize);
            double z = SectorOrigin.y + (zIndex * inv * SectorSize);
            float h = SampleHeight(x, z, BaseHeight, Seed);
            float hx = SampleHeight(x + 1.0, z, BaseHeight, Seed);
            float hz = SampleHeight(x, z + 1.0, BaseHeight, Seed);
            float3 normal = SafeNormalize(new float3(h - hx, 1f, h - hz), new float3(0f, 1f, 0f));

            GeologyTerrainSampleDTO sample = default;
            sample.Height = h;
            sample.Normal = normal;
            sample.AbsoluteX = x;
            sample.AbsoluteZ = z;
            Samples[index] = sample;
        }

        private static float SampleHeight(double x, double z, float baseHeight, uint seed)
        {
            double seedPhase = (seed & 4095u) * 0.000244140625;
            float waveA = TriangleSigned((float)((x * 0.037) + (z * 0.011) + seedPhase));
            float waveB = TriangleSigned((float)((z * 0.023) - (x * 0.017)));
            float ridge = TriangleSigned((float)((x + z) * 0.0061));
            return baseHeight + (waveA * 3.5f) + (waveB * 1.75f) + (ridge * 0.65f);
        }

        private static float TriangleSigned(float phase)
        {
            float t = math.frac(phase);
            return 1f - math.abs((t * 4f) - 2f);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, 0.0001f));
        }
    }

    #if UNITY_EDITOR
    public static class ProceduralGeologyCsv
    {
        public static int ParseDistributionRules(ReadOnlySpan<byte> csvBytes, NativeArray<GeologyDistributionRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length == 0 || csvBytes.Length == 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            while (lineStart < csvBytes.Length && count < rules.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineEnd - lineStart);
                if (TryParseRule(line, out GeologyDistributionRuleDTO rule))
                    rules[count++] = rule;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return count;
        }

        private static bool TryParseRule(ReadOnlySpan<byte> line, out GeologyDistributionRuleDTO rule)
        {
            rule = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> biome = NextColumn(ref line);
            ReadOnlySpan<byte> item = NextColumn(ref line);
            ReadOnlySpan<byte> weightText = NextColumn(ref line);
            ReadOnlySpan<byte> minDepthText = NextColumn(ref line);
            ReadOnlySpan<byte> maxDepthText = NextColumn(ref line);

            if (biome.Length == 0 || item.Length == 0 || !TryParseInt(weightText, out int weight))
                return false;

            TryParseFloat(minDepthText, out float minDepth);
            TryParseFloat(maxDepthText, out float maxDepth);
            if (!TryResolveResourceType(item, out uint resourceType))
                return false;

            rule.BiomeHash = ProceduralGeologyHash.Fnv1A(biome);
            rule.ResourceTypeHash = resourceType;
            rule.Weight = math.max(0, weight);
            rule.MinDepth = minDepth;
            rule.MaxDepth = maxDepth <= minDepth ? float.MaxValue : maxDepth;
            rule.RuleHash = ProceduralGeologyHash.Fnv1A(item) ^ (ProceduralGeologyHash.Fnv1A(biome) * 16777619u);
            return rule.Weight > 0;
        }

        private static bool TryResolveResourceType(ReadOnlySpan<byte> item, out uint resourceType)
        {
            resourceType = 0u;
            item = Trim(item);
            if (TryParseInt(item, out int numericOreType))
            {
                if (numericOreType >= WorldOreTypeIds.BasaltIron && numericOreType <= WorldOreTypeIds.Silver)
                {
                    resourceType = (uint)numericOreType;
                    return true;
                }

                return false;
            }

            if (AsciiEquals(item, "basalt_iron") ||
                AsciiEquals(item, "basaltiron") ||
                AsciiEquals(item, "basalt_iron_ore") ||
                AsciiEquals(item, "iron") ||
                AsciiEquals(item, "iron_ore"))
            {
                resourceType = WorldOreTypeIds.BasaltIron;
                return true;
            }

            if (AsciiEquals(item, "copper") || AsciiEquals(item, "copper_ore"))
            {
                resourceType = WorldOreTypeIds.Copper;
                return true;
            }

            if (AsciiEquals(item, "titanium") || AsciiEquals(item, "titanium_ore"))
            {
                resourceType = WorldOreTypeIds.Titanium;
                return true;
            }

            if (AsciiEquals(item, "silver") || AsciiEquals(item, "silver_ore"))
            {
                resourceType = WorldOreTypeIds.Silver;
                return true;
            }

            return false;
        }

        private static bool AsciiEquals(ReadOnlySpan<byte> value, string token)
        {
            if (value.Length != token.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != token[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> NextColumn(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            ReadOnlySpan<byte> column;
            if (comma < 0)
            {
                column = line;
                line = ReadOnlySpan<byte>.Empty;
            }
            else
            {
                column = line.Slice(0, comma);
                line = line.Slice(comma + 1);
            }

            return Trim(column);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhite(value[start]))
                start++;
            while (end >= start && IsWhite(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool IsWhite(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool TryParseInt(ReadOnlySpan<byte> value, out int parsed)
        {
            parsed = 0;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (value[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }

            int result = 0;
            for (; index < value.Length; index++)
            {
                byte b = value[index];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                result = (result * 10) + (b - (byte)'0');
            }

            parsed = result * sign;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (value[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }

            double result = 0.0;
            while (index < value.Length)
            {
                byte b = value[index];
                if (b == (byte)'.')
                    break;
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                result = (result * 10.0) + (b - (byte)'0');
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                double place = 0.1;
                while (index < value.Length)
                {
                    byte b = value[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        return false;
                    result += (b - (byte)'0') * place;
                    place *= 0.1;
                    index++;
                }
            }

            parsed = (float)(result * sign);
            return math.isfinite(parsed);
        }
    }
    #endif

#pragma warning restore 0169
}
