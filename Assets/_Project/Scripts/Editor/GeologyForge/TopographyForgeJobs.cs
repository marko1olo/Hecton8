#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.GeologyForge
{
    internal static class TopographyNoiseMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateRidgedMultifractal(double2 aupXZ, FractalParamsDTO parameters, float ridgeBlend)
        {
            float amplitude = math.max(0f, parameters.Amplitude);
            double frequency = math.max(0.0000001f, parameters.Frequency);
            double lacunarity = math.max(1.0001f, parameters.Lacunarity);
            float persistence = math.saturate(parameters.Persistence);
            int octaves = math.clamp(parameters.Octaves, 1, 12);
            float sum = 0f;
            float norm = 0f;
            float weight = 1f;

            for (int i = 0; i < octaves; i++)
            {
                uint seed = parameters.SeedHash + (uint)(i * 0x9E3779B9u);
                float n = ValueNoiseSigned(aupXZ * frequency, seed);
                float ridged = 1f - math.abs(n);
                ridged *= ridged;
                float folded = math.lerp(n * 0.5f + 0.5f, ridged, math.saturate(ridgeBlend));
                sum += folded * amplitude * weight;
                norm += weight;
                frequency *= lacunarity;
                weight *= persistence;
            }

            return math.saturate(sum * math.rcp(math.max(0.000001f, norm)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxPow01Curve(float value01, float exponent)
        {
            float x = math.saturate(math.select(0f, value01, math.isfinite(value01)));
            float e = math.clamp(math.select(1f, exponent, math.isfinite(exponent)), 0.25f, 4f);
            float sqrt1 = math.sqrt(x);
            float sqrt2 = math.sqrt(sqrt1);
            float x2 = x * x;
            float x3 = x2 * x;
            float x4 = x2 * x2;
            float r025To05 = math.lerp(sqrt2, sqrt1, math.saturate((e - 0.25f) * 4f));
            float r05To1 = math.lerp(sqrt1, x, math.saturate((e - 0.5f) * 2f));
            float r1To2 = math.lerp(x, x2, math.saturate(e - 1f));
            float r2To3 = math.lerp(x2, x3, math.saturate(e - 2f));
            float r3To4 = math.lerp(x3, x4, math.saturate(e - 3f));
            float result = r3To4;
            result = math.select(result, r2To3, e < 3f);
            result = math.select(result, r1To2, e < 2f);
            result = math.select(result, r05To1, e < 1f);
            result = math.select(result, r025To05, e < 0.5f);
            return math.saturate(math.select(0f, result, math.isfinite(result)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 EvaluateDomainWarp(double2 aupXZ, DomainWarpParamsDTO parameters)
        {
            double frequency = math.max(0.0000001f, parameters.Frequency);
            double lacunarity = math.max(1.0001f, parameters.Lacunarity);
            float persistence = math.saturate(parameters.Persistence);
            int octaves = math.clamp(parameters.Octaves, 1, 8);
            double2 warp = double2.zero;
            double amp = 1.0;
            double norm = 0.0;

            for (int i = 0; i < octaves; i++)
            {
                uint seedX = parameters.SeedHash ^ (uint)(0xA341316Cu + i * 0x85EBCA6Bu);
                uint seedZ = parameters.SeedHash ^ (uint)(0xC8013EA4u + i * 0xC2B2AE35u);
                warp.x += ValueNoiseSigned(aupXZ * frequency + new double2(17.371, -31.113), seedX) * amp;
                warp.y += ValueNoiseSigned(aupXZ * frequency + new double2(-23.517, 43.731), seedZ) * amp;
                norm += amp;
                amp *= persistence;
                frequency *= lacunarity;
            }

            double inv = 1.0 / math.max(0.000001, norm);
            return warp * inv * math.max(0f, parameters.StrengthMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ValueNoiseSigned(double2 sample, uint seed)
        {
            return (ValueNoise01(sample, seed) * 2f) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ValueNoise01(double2 sample, uint seed)
        {
            if (!math.all(math.isfinite(sample)))
                return 0.5f;

            sample = math.clamp(sample, new double2(-9007199254740991.0), new double2(9007199254740991.0));
            double2 floorSample = math.floor(sample);
            long x0 = (long)floorSample.x;
            long z0 = (long)floorSample.y;
            double2 local = sample - floorSample;
            double2 smooth = local * local * (3.0 - (2.0 * local));
            float a = Hash01(x0, z0, seed);
            float b = Hash01(x0 + 1L, z0, seed);
            float c = Hash01(x0, z0 + 1L, seed);
            float d = Hash01(x0 + 1L, z0 + 1L, seed);
            float xA = math.lerp(a, b, (float)smooth.x);
            float xB = math.lerp(c, d, (float)smooth.x);
            return math.lerp(xA, xB, (float)smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashAup(double3 aup, uint seed)
        {
            uint hash = Mix(seed ^ 2166136261u);
            hash = HashDoubleLane(aup.x, hash);
            hash = HashDoubleLane(aup.y, hash);
            hash = HashDoubleLane(aup.z, hash);
            return Mix(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashDoubleLane(double value, uint hash)
        {
            long bits = math.aslong(value == 0.0 ? 0.0 : value);
            ulong raw = (ulong)bits;
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(raw >> shift);
                hash *= 16777619u;
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(long x, long z, uint seed)
        {
            ulong h = (ulong)x * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)z * 0xC2B2AE3D27D4EB4FUL;
            h ^= seed;
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return (uint)(h >> 40) * (1f / 16777215f);
        }
    }

    internal static class TopographyQualityMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQuality(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            return q * q * (3f - (2f * q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FractalParamsDTO ApplyRidgeQuality(FractalParamsDTO source, float globalQualityWeight)
        {
            float q = ResolveQuality(globalQualityWeight);
            int maxOctaves = math.clamp(source.Octaves, 1, 12);
            FractalParamsDTO target = source;
            target.Octaves = math.clamp((int)math.round(math.lerp(2f, maxOctaves, q)), 1, maxOctaves);
            target.Lacunarity = math.max(1.0001f, math.lerp(1.62f, source.Lacunarity, q));
            target.Persistence = math.saturate(math.lerp(math.min(source.Persistence, 0.42f), source.Persistence, q));
            return target;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DomainWarpParamsDTO ApplyWarpQuality(DomainWarpParamsDTO source, float globalQualityWeight)
        {
            float q = ResolveQuality(globalQualityWeight);
            int maxOctaves = math.clamp(source.Octaves, 1, 8);
            DomainWarpParamsDTO target = source;
            target.Octaves = math.clamp((int)math.round(math.lerp(1f, maxOctaves, q)), 1, maxOctaves);
            target.StrengthMeters = math.max(0f, math.lerp(source.StrengthMeters * 0.18f, source.StrengthMeters, q));
            target.Lacunarity = math.max(1.0001f, math.lerp(1.45f, source.Lacunarity, q));
            target.Persistence = math.saturate(math.lerp(math.min(source.Persistence, 0.35f), source.Persistence, q));
            return target;
        }
    }

    internal static class TopographyBiomeBlendMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FractalParamsDTO ResolveRidge(
            double2 aupXZ,
            FractalParamsDTO fallback,
            NativeArray<TopographyBiomeKernelDTO> recipes,
            out float ridgeBlend)
        {
            ridgeBlend = 1f;
            if (!recipes.IsCreated || recipes.Length == 0)
                return fallback;

            double weightSum = 0.0;
            float frequency = 0f;
            float amplitude = 0f;
            float lacunarity = 0f;
            float persistence = 0f;
            float octaves = 0f;
            float blend = 0f;
            uint seed = fallback.SeedHash;
            float bestWeight = -1f;

            for (int i = 0; i < recipes.Length; i++)
            {
                TopographyBiomeKernelDTO recipe = recipes[i];
                float w = ResolveWeight(aupXZ, recipe.CenterAupXZ, recipe.InvRadiusSqMeters);
                if (w <= 0.000001f)
                    continue;

                weightSum += w;
                frequency += recipe.Ridge.Frequency * w;
                amplitude += recipe.Ridge.Amplitude * w;
                lacunarity += recipe.Ridge.Lacunarity * w;
                persistence += recipe.Ridge.Persistence * w;
                octaves += recipe.Ridge.Octaves * w;
                blend += recipe.RidgeBlend * w;
                if (w > bestWeight)
                {
                    bestWeight = w;
                    seed = recipe.SeedHash != 0u ? recipe.SeedHash : recipe.Ridge.SeedHash;
                }
            }

            if (weightSum <= 0.000001)
                return fallback;

            float inv = (float)(1.0 / weightSum);
            FractalParamsDTO resolved = default;
            resolved.Frequency = math.max(0.0000001f, frequency * inv);
            resolved.Amplitude = math.max(0f, amplitude * inv);
            resolved.Lacunarity = math.max(1.0001f, lacunarity * inv);
            resolved.Persistence = math.saturate(persistence * inv);
            resolved.Octaves = math.clamp((int)math.round(octaves * inv), 1, 12);
            resolved.SeedHash = seed != 0u ? seed : fallback.SeedHash;
            ridgeBlend = math.saturate(blend * inv);
            return resolved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DomainWarpParamsDTO ResolveWarp(
            double2 aupXZ,
            DomainWarpParamsDTO fallback,
            NativeArray<TopographyBiomeKernelDTO> recipes)
        {
            if (!recipes.IsCreated || recipes.Length == 0)
                return fallback;

            double weightSum = 0.0;
            float frequency = 0f;
            float strength = 0f;
            float lacunarity = 0f;
            float persistence = 0f;
            float octaves = 0f;
            uint seed = fallback.SeedHash;
            float bestWeight = -1f;

            for (int i = 0; i < recipes.Length; i++)
            {
                TopographyBiomeKernelDTO recipe = recipes[i];
                float w = ResolveWeight(aupXZ, recipe.CenterAupXZ, recipe.InvRadiusSqMeters);
                if (w <= 0.000001f)
                    continue;

                weightSum += w;
                frequency += recipe.Warp.Frequency * w;
                strength += recipe.Warp.StrengthMeters * w;
                lacunarity += recipe.Warp.Lacunarity * w;
                persistence += recipe.Warp.Persistence * w;
                octaves += recipe.Warp.Octaves * w;
                if (w > bestWeight)
                {
                    bestWeight = w;
                    seed = recipe.SeedHash != 0u ? recipe.SeedHash : recipe.Warp.SeedHash;
                }
            }

            if (weightSum <= 0.000001)
                return fallback;

            float inv = (float)(1.0 / weightSum);
            DomainWarpParamsDTO resolved = default;
            resolved.Frequency = math.max(0.0000001f, frequency * inv);
            resolved.StrengthMeters = math.max(0f, strength * inv);
            resolved.Lacunarity = math.max(1.0001f, lacunarity * inv);
            resolved.Persistence = math.saturate(persistence * inv);
            resolved.Octaves = math.clamp((int)math.round(octaves * inv), 1, 8);
            resolved.SeedHash = seed != 0u ? seed : fallback.SeedHash;
            return resolved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWeight(double2 aupXZ, double2 centerAupXZ, float invRadiusSqMeters)
        {
            double2 delta = aupXZ - centerAupXZ;
            double distanceSq = math.max(0.0, math.dot(delta, delta));
            float t = math.saturate(1f - ((float)distanceSq * math.max(0.000000000001f, invRadiusSqMeters)));
            return t * t * (3f - (2f * t));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMockSectorJob : IJobParallelFor
    {
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> HeightsMeters;
        public TopographyBakeConfigDTO Config;
        public FractalParamsDTO Ridge;

        public void Execute(int index)
        {
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(HeightsMeters);
            ref float height = ref UnsafeUtility.AsRef<float>(ptr + index);
            double2 aup = ResolveAupXZ(index);
            float ridge01 = TopographyNoiseMath.EvaluateRidgedMultifractal(aup, Ridge, Config.RidgeBlend);
            float h = Config.SeaFloorBiasMeters + math.lerp(Config.HeightMinMeters, Config.HeightMaxMeters, ridge01) * math.max(0.0001f, Config.HeightScaleMeters);
            height = math.isfinite(h) ? math.clamp(h, Config.HeightMinMeters, Config.HeightMaxMeters) : Config.HeightMinMeters;
        }

        private double2 ResolveAupXZ(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            return new double2(
                Config.SectorAup.x + (x * Config.PixelSizeMeters),
                Config.SectorAup.z + (z * Config.PixelSizeMeters));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyDomainWarpingJob : IJobParallelFor
    {
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<double2> WarpedAupXZ;
        [ReadOnly, NoAlias] public NativeArray<TopographyBiomeKernelDTO> Recipes;
        public TopographyBakeConfigDTO Config;
        public DomainWarpParamsDTO Warp;

        public void Execute(int index)
        {
            double2 aup = ResolveAupXZ(index);
            DomainWarpParamsDTO warp = TopographyBiomeBlendMath.ResolveWarp(aup, Warp, Recipes);
            double2 warped = aup + TopographyNoiseMath.EvaluateDomainWarp(aup, warp);
            double2* ptr = (double2*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(WarpedAupXZ);
            ref double2 target = ref UnsafeUtility.AsRef<double2>(ptr + index);
            target = math.all(math.isfinite(warped)) ? warped : aup;
        }

        private double2 ResolveAupXZ(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            return new double2(
                Config.SectorAup.x + (x * Config.PixelSizeMeters),
                Config.SectorAup.z + (z * Config.PixelSizeMeters));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateMountainRidgesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<double2> WarpedAupXZ;
        [ReadOnly, NoAlias] public NativeArray<TopographyBiomeKernelDTO> Recipes;
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> HeightsMeters;
        public TopographyBakeConfigDTO Config;
        public FractalParamsDTO Ridge;

        public void Execute(int index)
        {
            double2 aup = WarpedAupXZ.IsCreated && (uint)index < (uint)WarpedAupXZ.Length ? WarpedAupXZ[index] : ResolveAupXZ(index);
            FractalParamsDTO ridge = TopographyBiomeBlendMath.ResolveRidge(aup, Ridge, Recipes, out float ridgeBlend);
            float ridge01 = TopographyNoiseMath.EvaluateRidgedMultifractal(aup, ridge, ridgeBlend);
            float shelf = math.lerp(Config.HeightMinMeters, Config.HeightMaxMeters, ridge01);
            float h = Config.SeaFloorBiasMeters + shelf;
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(HeightsMeters);
            ref float target = ref UnsafeUtility.AsRef<float>(ptr + index);
            target = math.isfinite(h) ? math.clamp(h, Config.HeightMinMeters, Config.HeightMaxMeters) : Config.HeightMinMeters;
        }

        private double2 ResolveAupXZ(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            return new double2(
                Config.SectorAup.x + (x * Config.PixelSizeMeters),
                Config.SectorAup.z + (z * Config.PixelSizeMeters));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyStrataTerracingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeightsMeters;
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> OutputHeightsMeters;
        public TopographyBakeConfigDTO Config;

        public void Execute(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            float raw = InputHeightsMeters[index];
            float steps = math.max(1f, Config.TerraceSteps);
            float invRange = math.rcp(math.max(0.001f, Config.HeightMaxMeters - Config.HeightMinMeters));
            float h01 = math.saturate((raw - Config.HeightMinMeters) * invRange);
            float stepped01 = math.round(h01 * steps) * math.rcp(steps);
            float terraced = math.lerp(Config.HeightMinMeters, Config.HeightMaxMeters, stepped01);
            float slope = ResolveSlope(x, z);
            float slopeMask = math.smoothstep(Config.TerraceSlopeStart, math.max(Config.TerraceSlopeStart + 0.001f, Config.TerraceSlopeEnd), slope);
            float blend = math.saturate(Config.TerraceStrength) * slopeMask;
            float result = math.lerp(raw, terraced, blend);
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputHeightsMeters);
            ref float target = ref UnsafeUtility.AsRef<float>(ptr + index);
            target = math.isfinite(result) ? math.clamp(result, Config.HeightMinMeters, Config.HeightMaxMeters) : Config.HeightMinMeters;
        }

        private float ResolveSlope(int x, int z)
        {
            int west = math.max(0, x - 1) + (z * Config.Width);
            int east = math.min(Config.Width - 1, x + 1) + (z * Config.Width);
            int south = x + (math.max(0, z - 1) * Config.Width);
            int north = x + (math.min(Config.Height - 1, z + 1) * Config.Width);
            float dx = math.abs(InputHeightsMeters[east] - InputHeightsMeters[west]);
            float dz = math.abs(InputHeightsMeters[north] - InputHeightsMeters[south]);
            float invCell = (float)(0.5 / math.max(0.001, Config.PixelSizeMeters));
            return math.max(dx, dz) * invCell;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyTectonicRiftsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeightsMeters;
        [ReadOnly, NoAlias] public NativeArray<TectonicRiftSegmentDTO> Rifts;
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> OutputHeightsMeters;
        public TopographyBakeConfigDTO Config;

        public void Execute(int index)
        {
            double2 aup = ResolveAupXZ(index);
            float source = InputHeightsMeters[index];
            float carve = 0f;
            int count = math.clamp(Config.RiftCount, 0, Rifts.IsCreated ? Rifts.Length : 0);
            for (int i = 0; i < count; i++)
            {
                TectonicRiftSegmentDTO rift = Rifts[i];
                float width = math.max(0.001f, math.select(Config.RiftWidthMeters, rift.WidthMeters, rift.WidthMeters > 0f));
                double distanceSq = DistanceSqToSegment(aup, rift.StartAupXZ, rift.EndAupXZ);
                float invWidthSq = math.rcp(width * width);
                float t = math.saturate(1f - ((float)distanceSq * invWidthSq));
                float edge = TopographyNoiseMath.ApproxPow01Curve(t, math.max(0.25f, rift.FalloffPower));
                float sharpened = math.smoothstep(0f, 1f, edge);
                float depth = math.max(0f, math.select(Config.RiftDepthMeters, rift.DepthMeters, rift.DepthMeters > 0f));
                carve = math.max(carve, depth * sharpened);
            }

            float result = source - carve;
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputHeightsMeters);
            ref float target = ref UnsafeUtility.AsRef<float>(ptr + index);
            target = math.isfinite(result) ? math.clamp(result, Config.HeightMinMeters, Config.HeightMaxMeters) : Config.HeightMinMeters;
        }

        private double2 ResolveAupXZ(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            return new double2(
                Config.SectorAup.x + (x * Config.PixelSizeMeters),
                Config.SectorAup.z + (z * Config.PixelSizeMeters));
        }

        private static double DistanceSqToSegment(double2 p, double2 a, double2 b)
        {
            double2 ab = b - a;
            double denom = math.max(0.000001, math.dot(ab, ab));
            double t = math.clamp(math.dot(p - a, ab) / denom, 0.0, 1.0);
            double2 q = a + (ab * t);
            double2 d = p - q;
            return math.max(0.0, math.dot(d, d));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMacroHeightmapJob : IJobParallelFor
    {
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> MacroHeightsMeters;
        [ReadOnly, NoAlias] public NativeArray<TectonicRiftSegmentDTO> Rifts;
        [ReadOnly, NoAlias] public NativeArray<TopographyBiomeKernelDTO> Recipes;
        public TopographyBakeConfigDTO Config;
        public FractalParamsDTO Ridge;
        public DomainWarpParamsDTO Warp;
        public double2 WorldSizeMeters;

        public void Execute(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            double px = x * (1.0 / math.max(1, Config.Width - 1));
            double pz = z * (1.0 / math.max(1, Config.Height - 1));
            double2 aup = new double2(
                Config.SectorAup.x + (px * WorldSizeMeters.x),
                Config.SectorAup.z + (pz * WorldSizeMeters.y));
            DomainWarpParamsDTO warp = TopographyBiomeBlendMath.ResolveWarp(aup, Warp, Recipes);
            FractalParamsDTO ridge = TopographyBiomeBlendMath.ResolveRidge(aup, Ridge, Recipes, out float ridgeBlend);
            double2 warped = aup + TopographyNoiseMath.EvaluateDomainWarp(aup, warp);
            float ridge01 = TopographyNoiseMath.EvaluateRidgedMultifractal(warped, ridge, ridgeBlend);
            float raw = Config.SeaFloorBiasMeters + math.lerp(Config.HeightMinMeters, Config.HeightMaxMeters, ridge01);
            float carve = 0f;
            int count = math.clamp(Config.RiftCount, 0, Rifts.IsCreated ? Rifts.Length : 0);
            for (int i = 0; i < count; i++)
            {
                TectonicRiftSegmentDTO rift = Rifts[i];
                float width = math.max(0.001f, math.select(Config.RiftWidthMeters, rift.WidthMeters, rift.WidthMeters > 0f));
                double dSq = DistanceSqToSegment(aup, rift.StartAupXZ, rift.EndAupXZ);
                float invWidthSq = math.rcp(width * width);
                float t = math.saturate(1f - ((float)dSq * invWidthSq));
                float edge = TopographyNoiseMath.ApproxPow01Curve(t, math.max(0.25f, rift.FalloffPower));
                float sharpened = math.smoothstep(0f, 1f, edge);
                float depth = math.max(0f, math.select(Config.RiftDepthMeters, rift.DepthMeters, rift.DepthMeters > 0f));
                carve = math.max(carve, depth * sharpened);
            }

            float result = math.clamp(raw - carve, Config.HeightMinMeters, Config.HeightMaxMeters);
            float* ptr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(MacroHeightsMeters);
            ref float target = ref UnsafeUtility.AsRef<float>(ptr + index);
            target = math.isfinite(result) ? result : Config.HeightMinMeters;
        }

        private static double DistanceSqToSegment(double2 p, double2 a, double2 b)
        {
            double2 ab = b - a;
            double denom = math.max(0.000001, math.dot(ab, ab));
            double t = math.clamp(math.dot(p - a, ab) / denom, 0.0, 1.0);
            double2 q = a + (ab * t);
            double2 d = p - q;
            return math.max(0.0, math.dot(d, d));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateBiomeMaskJob : IJobParallelFor
    {
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float4> BiomeMaskWeights;
        [ReadOnly, NoAlias] public NativeArray<TopographyBiomeKernelDTO> Recipes;
        public TopographyBakeConfigDTO Config;

        public void Execute(int index)
        {
            double2 aup = ResolveAupXZ(index);
            float4 weights = ResolveWeights(aup);
            float4* ptr = (float4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BiomeMaskWeights);
            ref float4 target = ref UnsafeUtility.AsRef<float4>(ptr + index);
            target = math.all(math.isfinite(weights)) ? weights : new float4(1f, 0f, 0f, 0f);
        }

        private double2 ResolveAupXZ(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            return new double2(
                Config.SectorAup.x + (x * Config.PixelSizeMeters),
                Config.SectorAup.z + (z * Config.PixelSizeMeters));
        }

        private float4 ResolveWeights(double2 aup)
        {
            if (!Recipes.IsCreated || Recipes.Length == 0)
                return new float4(1f, 0f, 0f, 0f);

            float4 channels = new float4(
                ResolveChannelWeight(aup, 0),
                ResolveChannelWeight(aup, 1),
                ResolveChannelWeight(aup, 2),
                ResolveChannelWeight(aup, 3));
            float total = math.csum(channels);
            float inv = math.rcp(math.max(0.000001f, total));
            float4 normalized = math.saturate(channels * inv);
            float hasWeights = math.step(0.000001f, total);
            return math.lerp(new float4(1f, 0f, 0f, 0f), normalized, hasWeights);
        }

        private float ResolveChannelWeight(double2 aup, int channel)
        {
            if (channel >= Recipes.Length)
                return 0f;

            TopographyBiomeKernelDTO recipe = Recipes[channel];
            return TopographyBiomeBlendMath.ResolveWeight(aup, recipe.CenterAupXZ, recipe.InvRadiusSqMeters);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMacroBiomeMaskJob : IJobParallelFor
    {
        // Invariant: Execute(index) writes exactly index; unsafe ref store avoids NativeArray indexer copies.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float4> BiomeMaskWeights;
        [ReadOnly, NoAlias] public NativeArray<TopographyBiomeKernelDTO> Recipes;
        public TopographyBakeConfigDTO Config;
        public double2 WorldSizeMeters;

        public void Execute(int index)
        {
            int x = index % Config.Width;
            int z = index / Config.Width;
            double px = x * (1.0 / math.max(1, Config.Width - 1));
            double pz = z * (1.0 / math.max(1, Config.Height - 1));
            double2 aup = new double2(
                Config.SectorAup.x + (px * WorldSizeMeters.x),
                Config.SectorAup.z + (pz * WorldSizeMeters.y));
            float4 weights = ResolveWeights(aup);
            float4* ptr = (float4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BiomeMaskWeights);
            ref float4 target = ref UnsafeUtility.AsRef<float4>(ptr + index);
            target = math.all(math.isfinite(weights)) ? weights : new float4(1f, 0f, 0f, 0f);
        }

        private float4 ResolveWeights(double2 aup)
        {
            if (!Recipes.IsCreated || Recipes.Length == 0)
                return new float4(1f, 0f, 0f, 0f);

            float4 channels = new float4(
                ResolveChannelWeight(aup, 0),
                ResolveChannelWeight(aup, 1),
                ResolveChannelWeight(aup, 2),
                ResolveChannelWeight(aup, 3));
            float total = math.csum(channels);
            float inv = math.rcp(math.max(0.000001f, total));
            float4 normalized = math.saturate(channels * inv);
            float hasWeights = math.step(0.000001f, total);
            return math.lerp(new float4(1f, 0f, 0f, 0f), normalized, hasWeights);
        }

        private float ResolveChannelWeight(double2 aup, int channel)
        {
            if (channel >= Recipes.Length)
                return 0f;

            TopographyBiomeKernelDTO recipe = Recipes[channel];
            return TopographyBiomeBlendMath.ResolveWeight(aup, recipe.CenterAupXZ, recipe.InvRadiusSqMeters);
        }
    }
}
#endif
