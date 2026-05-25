#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.BiomeWeightMapBaker.Editor
{
    public static class BiomeWeightMapBakeConstants
    {
        public const int DefaultResolution = 2048;
        public const int PreviewResolution = 256;
        public const int MaxResolution = 4096;
        public const int DefaultRulesPerMacro = 4;
        public const int MaxRuleCount = 64;
        public const int TelemetryFrames = 300;
        public const uint RollbackExcludedFlag = 1u;
        public const uint WarningNonFiniteColor = 1u << 1;
        public const uint WarningBc7CompressionFailed = 1u << 2;
        public const uint DumpMagic = 0x53424D57u;
        public const uint ReportVersion = 1u;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BiomeBlendRuleDTO
    {
        [FieldOffset(0)] public float MinHeight;
        [FieldOffset(4)] public float MaxHeight;
        [FieldOffset(8)] public float MinSlope;
        [FieldOffset(12)] public float MaxSlope;
        [FieldOffset(16)] public float NoiseFrequency;
        [FieldOffset(20)] public float BlendSoftness;
        [FieldOffset(24)] public uint ChannelIndex;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BiomeSplatmapBakeConfigDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAUP;
        [FieldOffset(24)] public int Width;
        [FieldOffset(28)] public int Height;
        [FieldOffset(32)] public float CellSizeMeters;
        [FieldOffset(36)] public float HeightScaleMeters;
        [FieldOffset(40)] public float NoiseStrength;
        [FieldOffset(44)] public float NoiseFrequency;
        [FieldOffset(48)] public float ErosionOverrideThreshold;
        [FieldOffset(52)] public float ErosionBlendSoftness;
        [FieldOffset(56)] public int MacroWidth;
        [FieldOffset(60)] public int MacroHeight;
        [FieldOffset(64)] public int RulesPerMacro;
        [FieldOffset(68)] public int RuleSetCount;
        [FieldOffset(72)] public uint Seed;
        [FieldOffset(76)] public float GlobalQualityWeight;
        [FieldOffset(80)] public int BlurRadiusPixels;
        [FieldOffset(84)] public uint EdgeSampleFlags;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public uint _pad0;
        [FieldOffset(96)] public ulong _pad1;
        [FieldOffset(104)] public ulong _pad2;
        [FieldOffset(112)] public ulong _pad3;
        [FieldOffset(120)] public ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeSplatmapBakeTelemetryEntry
    {
        [FieldOffset(0)] public uint Stage;
        [FieldOffset(4)] public uint PixelCount;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public uint WarningFlags;
        [FieldOffset(16)] public double SectorOriginX;
        [FieldOffset(24)] public double SectorOriginY;
        [FieldOffset(32)] public double SectorOriginZ;
        [FieldOffset(40)] public float NormalMilliseconds;
        [FieldOffset(44)] public float WeightMilliseconds;
        [FieldOffset(48)] public float SerializationMilliseconds;
        [FieldOffset(52)] public int NonFiniteCount;
        [FieldOffset(56)] public int Width;
        [FieldOffset(60)] public int Height;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockHeightmapJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Heights01;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Erosion01;
        [ReadOnly] public BiomeSplatmapBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            if ((uint)index >= (uint)(width * height))
                return;

            int x = index % width;
            int z = index / width;
            double cell = math.max(0.001d, Config.CellSizeMeters);
            double3 sampleAup = Config.SectorOriginAUP + new double3(x * cell, 0.0d, z * cell);
            float nx = ((float)x / math.max(1f, width - 1f)) * 2f - 1f;
            float nz = ((float)z / math.max(1f, height - 1f)) * 2f - 1f;
            float quality = BiomeWeightMapBakeMath.QualityCurve(Config.GlobalQualityWeight);
            float ridge = BiomeWeightMapBakeMath.RidgedNoise2Quality(sampleAup, Config.SectorOriginAUP, Config.Seed ^ 0x9E3779B9u, 0.00042f, quality);
            float detail = BiomeWeightMapBakeMath.FractalNoise2Quality(sampleAup, Config.SectorOriginAUP, Config.Seed ^ 0xC001CAFEu, 0.0017f, quality);
            float canyonAxis = math.abs(nx + Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(nz * 5.3f) * 0.18f);
            float canyon = 1f - math.smoothstep(0.035f, 0.18f, canyonAxis);
            float shelf = math.smoothstep(-0.9f, 0.45f, -nz);
            float cliffBand = math.smoothstep(0.35f, 0.62f, math.abs(nx - 0.22f));
            float heightDetail = math.lerp(0.08f, 0.16f, quality);
            float height01 = math.saturate(0.58f + ridge * 0.28f + detail * heightDetail + shelf * 0.12f - canyon * 0.44f - cliffBand * 0.18f);
            float sediment = math.saturate(canyon * (1f - ridge * 0.35f) + (1f - math.abs(nz)) * 0.08f + detail * 0.12f);

            float* hPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Heights01);
            float* ePtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Erosion01);
            UnsafeUtility.AsRef<float>(hPtr + index) = height01;
            UnsafeUtility.AsRef<float>(ePtr + index) = sediment;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockMacroBiomeJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> MacroBiomeHashes;
        [ReadOnly] public BiomeSplatmapBakeConfigDTO Config;

        public void Execute(int index)
        {
            int macroWidth = math.max(1, Config.MacroWidth);
            int macroHeight = math.max(1, Config.MacroHeight);
            if ((uint)index >= (uint)MacroBiomeHashes.Length)
                return;

            int x = index % macroWidth;
            int z = index / macroWidth;
            double widthMeters = math.max(1.0d, Config.Width * math.max(0.001d, Config.CellSizeMeters));
            double heightMeters = math.max(1.0d, Config.Height * math.max(0.001d, Config.CellSizeMeters));
            double sx = ((x + 0.5d) / macroWidth) * widthMeters;
            double sz = ((z + 0.5d) / macroHeight) * heightMeters;
            double3 macroAup = Config.SectorOriginAUP + new double3(sx, 0.0d, sz);
            float quality = BiomeWeightMapBakeMath.QualityCurve(Config.GlobalQualityWeight);
            float macroNoise = BiomeWeightMapBakeMath.FractalNoise2Quality(
                macroAup,
                Config.SectorOriginAUP,
                Config.Seed ^ 0x4D414352u,
                math.lerp(0.00009f, 0.00023f, quality),
                quality);
            uint macro = (uint)math.clamp((int)math.floor(macroNoise * math.max(1, Config.RuleSetCount)), 0, math.max(0, Config.RuleSetCount - 1));
            uint* macroPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(MacroBiomeHashes);
            UnsafeUtility.AsRef<uint>(macroPtr + index) = macro;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateTerrainNormalsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights01;
        [ReadOnly, NoAlias] public NativeArray<float> WestEdgeHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> EastEdgeHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> SouthEdgeHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> NorthEdgeHeights01;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float3> Normals;
        [ReadOnly] public BiomeSplatmapBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            if ((uint)index >= (uint)Normals.Length)
                return;

            int x = index % width;
            int z = index / width;
            float west = ReadHeight(x - 1, z, width, height);
            float east = ReadHeight(x + 1, z, width, height);
            float south = ReadHeight(x, z - 1, width, height);
            float north = ReadHeight(x, z + 1, width, height);
            float heightScale = math.max(0.001f, Config.HeightScaleMeters);
            float invSpan = 0.5f / math.max(0.001f, Config.CellSizeMeters);
            float dx = (east - west) * heightScale * invSpan;
            float dz = (north - south) * heightScale * invSpan;
            float3 normal = math.normalizesafe(new float3(-dx, 1f, -dz), new float3(0f, 1f, 0f));
            float3* normalPtr = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Normals);
            UnsafeUtility.AsRef<float3>(normalPtr + index) = normal;
        }

        private float ReadHeight(int x, int z, int width, int height)
        {
            if (x < 0)
            {
                if ((Config.EdgeSampleFlags & 1u) != 0u && (uint)z < (uint)WestEdgeHeights01.Length)
                    return WestEdgeHeights01[z];
                x = 0;
            }
            else if (x >= width)
            {
                if ((Config.EdgeSampleFlags & 2u) != 0u && (uint)z < (uint)EastEdgeHeights01.Length)
                    return EastEdgeHeights01[z];
                x = width - 1;
            }

            if (z < 0)
            {
                if ((Config.EdgeSampleFlags & 4u) != 0u && (uint)x < (uint)SouthEdgeHeights01.Length)
                    return SouthEdgeHeights01[x];
                z = 0;
            }
            else if (z >= height)
            {
                if ((Config.EdgeSampleFlags & 8u) != 0u && (uint)x < (uint)NorthEdgeHeights01.Length)
                    return NorthEdgeHeights01[x];
                z = height - 1;
            }

            int sourceIndex = z * width + x;
            if ((uint)sourceIndex >= (uint)Heights01.Length)
                return 0f;

            float* hPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Heights01);
            return math.saturate(UnsafeUtility.AsRef<float>(hPtr + sourceIndex));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateBiomeWeightsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights01;
        [ReadOnly, NoAlias] public NativeArray<float3> Normals;
        [ReadOnly, NoAlias] public NativeArray<float> Erosion01;
        [ReadOnly, NoAlias] public NativeArray<uint> MacroBiomeHashes;
        [ReadOnly, NoAlias] public NativeArray<BiomeBlendRuleDTO> Rules;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<Color32> Pixels;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> NonFiniteFlags;
        [ReadOnly] public BiomeSplatmapBakeConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.Width);
            int height = math.max(1, Config.Height);
            if ((uint)index >= (uint)Pixels.Length)
                return;

            int x = index % width;
            int z = index / width;
            float height01 = ReadFloat(Heights01, index);
            float3 normal = ReadNormal(index);
            float slopeDegrees = math.degrees(global::Hecton8.Core.MathLodApproximation.ApproxAcosFast(math.clamp(normal.y, -1f, 1f)));
            double cell = math.max(0.001d, Config.CellSizeMeters);
            double3 pixelAup = Config.SectorOriginAUP + new double3(x * cell, height01 * Config.HeightScaleMeters, z * cell);
            float quality = BiomeWeightMapBakeMath.QualityCurve(Config.GlobalQualityWeight);
            float transitionNoise = BiomeWeightMapBakeMath.FractalNoise2Quality(
                pixelAup,
                Config.SectorOriginAUP,
                Config.Seed ^ 0xBAADF00Du,
                math.max(Config.NoiseFrequency, 0.000001f),
                quality);
            float qualityNoiseGain = math.lerp(0.35f, 1f, quality);
            float noiseOffset01 = (transitionNoise - 0.5f) * math.max(0f, Config.NoiseStrength) * qualityNoiseGain;
            float noisedHeight = math.saturate(height01 + noiseOffset01);
            float noisedSlope = math.clamp(slopeDegrees + noiseOffset01 * 90f, 0f, 90f);

            int ruleSet = ResolveRuleSet(x, z, width, height);
            float4 weights = EvaluateRules(ruleSet, noisedHeight, noisedSlope);
            float erosion = math.saturate(ReadFloat(Erosion01, index));
            float erosionSoft = math.max(0.0001f, Config.ErosionBlendSoftness);
            float erosionMask = math.smoothstep(
                math.saturate(Config.ErosionOverrideThreshold - erosionSoft),
                math.saturate(Config.ErosionOverrideThreshold + erosionSoft),
                erosion);

            weights.w = math.max(weights.w, erosionMask);
            float rgbScale = math.saturate(1f - weights.w);
            weights.x *= rgbScale;
            weights.y *= rgbScale;
            weights.z *= rgbScale;
            weights = NormalizeOrFallback(weights, noisedSlope, noisedHeight);

            byte finite = (byte)(math.all(math.isfinite(weights)) ? 0 : 1);
            if (finite != 0)
                weights = new float4(0f, 1f, 0f, 0f);

            Color32* pixelPtr = (Color32*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Pixels);
            UnsafeUtility.AsRef<Color32>(pixelPtr + index) = PackWeights(weights);

            if ((uint)index < (uint)NonFiniteFlags.Length)
            {
                byte* flagPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(NonFiniteFlags);
                UnsafeUtility.AsRef<byte>(flagPtr + index) = finite;
            }
        }

        private float4 EvaluateRules(int ruleSet, float height01, float slopeDegrees)
        {
            float4 weights = 0f;
            int rulesPerMacro = math.max(1, Config.RulesPerMacro);
            int start = math.clamp(ruleSet, 0, math.max(0, Config.RuleSetCount - 1)) * rulesPerMacro;
            int end = math.min(start + rulesPerMacro, Rules.Length);
            BiomeBlendRuleDTO* rulePtr = Rules.Length > 0 ? (BiomeBlendRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Rules) : null;

            for (int i = start; i < end; i++)
            {
                ref BiomeBlendRuleDTO rule = ref UnsafeUtility.AsRef<BiomeBlendRuleDTO>(rulePtr + i);
                float localNoise = BiomeWeightMapBakeMath.Hash01(i, ruleSet, (int)rule.ChannelIndex, Config.Seed) - 0.5f;
                float h = math.saturate(height01 + localNoise * math.max(0f, rule.NoiseFrequency) * Config.NoiseStrength);
                float heightWeight = BiomeWeightMapBakeMath.SoftWindow01(rule.MinHeight, rule.MaxHeight, h, math.max(0.0001f, rule.BlendSoftness));
                float slopeWeight = BiomeWeightMapBakeMath.SoftWindow01(rule.MinSlope, rule.MaxSlope, slopeDegrees, math.max(0.001f, rule.BlendSoftness * 90f));
                float weight = heightWeight * slopeWeight;
                uint channel = rule.ChannelIndex & 3u;
                weights.x += channel == 0u ? weight : 0f;
                weights.y += channel == 1u ? weight : 0f;
                weights.z += channel == 2u ? weight : 0f;
                weights.w += channel == 3u ? weight : 0f;
            }

            if (math.csum(weights) > 0.000001f)
                return weights;

            float rock = math.smoothstep(34f, 52f, slopeDegrees);
            float silt = math.saturate((1f - rock) * math.smoothstep(0.66f, 0.20f, height01));
            float sand = math.saturate((1f - rock) * (1f - silt));
            return new float4(rock, sand, silt, 0f);
        }

        private int ResolveRuleSet(int x, int z, int width, int height)
        {
            if (Config.RuleSetCount <= 1 || Config.MacroWidth <= 0 || Config.MacroHeight <= 0 || MacroBiomeHashes.Length <= 0)
                return 0;

            int mx = math.clamp((x * Config.MacroWidth) / math.max(1, width), 0, Config.MacroWidth - 1);
            int mz = math.clamp((z * Config.MacroHeight) / math.max(1, height), 0, Config.MacroHeight - 1);
            int macroIndex = mz * Config.MacroWidth + mx;
            if ((uint)macroIndex >= (uint)MacroBiomeHashes.Length)
                return 0;

            uint* macroPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(MacroBiomeHashes);
            uint hash = UnsafeUtility.AsRef<uint>(macroPtr + macroIndex);
            return (int)(hash % (uint)math.max(1, Config.RuleSetCount));
        }

        private float ReadFloat(NativeArray<float> values, int index)
        {
            if ((uint)index >= (uint)values.Length)
                return 0f;

            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(values);
            return math.saturate(UnsafeUtility.AsRef<float>(ptr + index));
        }

        private float3 ReadNormal(int index)
        {
            if ((uint)index >= (uint)Normals.Length)
                return new float3(0f, 1f, 0f);

            float3* ptr = (float3*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Normals);
            return math.normalizesafe(UnsafeUtility.AsRef<float3>(ptr + index), new float3(0f, 1f, 0f));
        }

        private static float4 NormalizeOrFallback(float4 weights, float slopeDegrees, float height01)
        {
            float total = math.csum(weights);
            if (total > 0.000001f)
                return weights / total;

            float rock = math.smoothstep(34f, 52f, slopeDegrees);
            float silt = math.saturate((1f - rock) * math.smoothstep(0.66f, 0.20f, height01));
            float sand = math.saturate((1f - rock) * (1f - silt));
            total = math.max(0.000001f, rock + sand + silt);
            return new float4(rock / total, sand / total, silt / total, 0f);
        }

        private static Color32 PackWeights(float4 weights)
        {
            weights = math.saturate(weights);
            weights /= math.max(0.000001f, math.csum(weights));
            int r = (int)math.round(weights.x * 255f);
            int g = (int)math.round(weights.y * 255f);
            int b = (int)math.round(weights.z * 255f);
            r = math.clamp(r, 0, 255);
            g = math.clamp(g, 0, 255);
            b = math.clamp(b, 0, 255);
            int a = 255 - r - g - b;
            if (a < 0)
            {
                int over = -a;
                int takeB = math.min(over, b);
                b -= takeB;
                over -= takeB;
                int takeG = math.min(over, g);
                g -= takeG;
                over -= takeG;
                r = math.max(0, r - over);
                a = 0;
            }

            return new Color32((byte)r, (byte)g, (byte)b, (byte)math.clamp(a, 0, 255));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct BoxBlurBiomeWeightsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<Color32> Source;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<Color32> Destination;
        [ReadOnly] public int Width;
        [ReadOnly] public int Height;
        [ReadOnly] public int Radius;

        public void Execute(int index)
        {
            int width = math.max(1, Width);
            int height = math.max(1, Height);
            if ((uint)index >= (uint)Destination.Length)
                return;

            int x = index % width;
            int z = index / width;
            int radius = math.clamp(Radius, 0, 8);
            int4 sum = 0;
            int count = 0;
            Color32* src = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);

            for (int oz = -radius; oz <= radius; oz++)
            {
                int sz = math.clamp(z + oz, 0, height - 1);
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int sx = math.clamp(x + ox, 0, width - 1);
                    Color32 c = UnsafeUtility.AsRef<Color32>(src + sz * width + sx);
                    sum += new int4(c.r, c.g, c.b, c.a);
                    count++;
                }
            }

            int safeCount = math.max(1, count);
            int r = sum.x / safeCount;
            int g = sum.y / safeCount;
            int b = sum.z / safeCount;
            int a = 255 - r - g - b;
            if (a < 0)
            {
                int over = -a;
                int takeB = math.min(over, b);
                b -= takeB;
                over -= takeB;
                int takeG = math.min(over, g);
                g -= takeG;
                over -= takeG;
                r = math.max(0, r - over);
                a = 0;
            }

            Color32* dst = (Color32*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Destination);
            UnsafeUtility.AsRef<Color32>(dst + index) = new Color32((byte)r, (byte)g, (byte)b, (byte)math.clamp(a, 0, 255));
        }
    }

    internal static class BiomeWeightMapBakeMath
    {
        public static float SoftWindow01(float minValue, float maxValue, float value, float softness)
        {
            float minEdge = math.min(minValue, maxValue);
            float maxEdge = math.max(minValue, maxValue);
            float soft = math.max(0.000001f, softness);
            float enter = math.smoothstep(minEdge - soft, minEdge + soft, value);
            float exit = 1f - math.smoothstep(maxEdge - soft, maxEdge + soft, value);
            return math.saturate(enter * exit);
        }

        public static float FractalNoise2Quality(double3 aup, double3 originAup, uint seed, float frequency, float qualityWeight)
        {
            float sum = 0f;
            float weightSum = 0f;
            float amp = 0.55f;
            float freq = math.max(0.000001f, frequency);
            float quality = QualityCurve(qualityWeight);
            int octaveCount = QualityOctaveCount(qualityWeight);
            for (int i = 0; i < octaveCount; i++)
            {
                sum += ValueNoise2(aup, originAup, freq, seed + (uint)(i * 1013)) * amp;
                weightSum += amp;
                freq *= 2.03f;
                amp *= 0.5f;
            }

            return math.saturate(sum / math.max(0.000001f, weightSum));
        }

        public static float QualityCurve(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return q * q * (3f - 2f * q);
        }

        public static int QualityOctaveCount(float qualityWeight)
        {
            float quality = QualityCurve(qualityWeight);
            return math.clamp(1 + (int)math.floor(quality * 3.999f), 1, 4);
        }

        public static float RidgedNoise2Quality(double3 aup, double3 originAup, uint seed, float frequency, float qualityWeight)
        {
            float value = FractalNoise2Quality(aup, originAup, seed, frequency, qualityWeight);
            float ridge = 1f - math.abs(value * 2f - 1f);
            return math.saturate(ridge * ridge);
        }

        public static float ValueNoise2(double3 aup, double3 originAup, float frequency, uint seed)
        {
            double localX = aup.x - originAup.x;
            double localZ = aup.z - originAup.z;
            double sx = originAup.x * frequency + localX * frequency;
            double sz = originAup.z * frequency + localZ * frequency;
            return ValueNoise2Scaled(sx, sz, seed);
        }

        private static float ValueNoise2Scaled(double sx, double sz, uint seed)
        {
            int ix = (int)math.floor(sx);
            int iz = (int)math.floor(sz);
            float fx = (float)(sx - ix);
            float fz = (float)(sz - iz);
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float v00 = Hash01(ix, 0, iz, seed);
            float v10 = Hash01(ix + 1, 0, iz, seed);
            float v01 = Hash01(ix, 0, iz + 1, seed);
            float v11 = Hash01(ix + 1, 0, iz + 1, seed);
            float x0 = math.lerp(v00, v10, fx);
            float x1 = math.lerp(v01, v11, fx);
            return math.lerp(x0, x1, fz);
        }

        public static float Hash01(int x, int y, int z, uint seed)
        {
            uint h = seed ^ 2166136261u;
            h = Mix(h ^ (uint)x * 374761393u);
            h = Mix(h ^ (uint)y * 668265263u);
            h = Mix(h ^ (uint)z * 2246822519u);
            return (h & 0x00FFFFFFu) * (1f / 16777216f);
        }

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }
}
#endif
