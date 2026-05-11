using System.Runtime.CompilerServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainTectonicDisplacementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public int Width;
        public int Height;
        public float2 WorldOriginXZ;
        public float CellSizeMeters;
        public float Strength01;
        public float Frequency;
        public float RidgeSharpness;
        public uint Seed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateTectonicBoundaryMask(float2 worldXZ, float frequency, uint seed)
        {
            float safeFrequency = math.max(0.0001f, frequency);
            float2 sample = worldXZ * safeFrequency;
            float2 warpedSample = sample + EvaluateDomainWarp(sample, seed);
            CellularData cell = EvaluateCellular(warpedSample, seed);
            float edgeDelta = math.max(0f, cell.F2 - cell.F1);
            float junctionDelta = math.max(0f, cell.F3 - cell.F2);
            float edgeMask = 1f - math.smoothstep(0.032f, 0.20f, edgeDelta);
            float junctionMask = 1f - math.smoothstep(0.026f, 0.16f, junctionDelta);
            return math.saturate(math.max(edgeMask, junctionMask));
        }

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            float source = math.saturate(InputHeights01[index]);
            float2 world = WorldOriginXZ + new float2(x, z) * math.max(0.001f, CellSizeMeters);
            float safeFrequency = math.max(0.0001f, Frequency);
            float2 sample = world * safeFrequency;
            float2 warpedSample = sample + EvaluateDomainWarp(sample, Seed);
            CellularData cell = EvaluateCellular(warpedSample, Seed);
            float edgeDelta = math.max(0f, cell.F2 - cell.F1);
            float junctionDelta = math.max(0f, cell.F3 - cell.F2);
            float edgeMask = 1f - math.smoothstep(0.032f, 0.20f, edgeDelta);
            float junctionMask = 1f - math.smoothstep(0.026f, 0.16f, junctionDelta);
            float forkNoise = FractalValueNoise(warpedSample * 0.41f + new float2(19.37f, -43.11f), Seed ^ 0x51633E2Du);
            float branching = math.saturate(edgeMask * 0.74f + junctionMask * 0.82f + math.smoothstep(0.42f, 0.9f, forkNoise) * 0.16f);
            float sharpenedRidge = math.pow(branching, math.max(0.5f, RidgeSharpness));

            float slabNoise = FractalValueNoise(warpedSample * 0.47f + new float2(37.13f, -19.71f), Seed ^ 0x9E3779B9u);
            float fracture = math.smoothstep(0.36f, 0.92f, math.abs(slabNoise * 2f - 1f));
            float ridgeMask = math.smoothstep(0.24f, 0.82f, sharpenedRidge);
            float extrusion = ridgeMask * math.lerp(0.62f, 1.18f, fracture) * math.saturate(Strength01);

            OutputHeights01[index] = math.saturate(source + extrusion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 EvaluateDomainWarp(float2 sample, uint seed)
        {
            float lowX = FractalValueNoise(sample * 0.31f + new float2(11.17f, -7.43f), seed ^ 0x5F356495u) * 2f - 1f;
            float lowY = FractalValueNoise(sample * 0.31f + new float2(-23.59f, 31.71f), seed ^ 0xC2B2AE35u) * 2f - 1f;
            float highX = FractalValueNoise(sample * 0.79f + new float2(47.7f, 3.19f), seed ^ 0xB5297A4Du) * 2f - 1f;
            float highY = FractalValueNoise(sample * 0.73f + new float2(-5.89f, 61.2f), seed ^ 0x68E31DA4u) * 2f - 1f;
            float twist = FractalValueNoise(sample * 0.23f + new float2(17.3f, -29.1f), seed ^ 0x1B56C4E9u) * 2f - 1f;
            float angle = twist * 1.0471976f;
            float s = CinematicMath.FastSin(angle);
            float c = CinematicMath.FastCos(angle);
            float2 warp = new float2(lowX, lowY) * 0.64f + new float2(highX, highY) * 0.36f;
            return new float2(warp.x * c - warp.y * s, warp.x * s + warp.y * c) * 0.72f;
        }

        private struct CellularData
        {
            public float F1;
            public float F2;
            public float F3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CellularData EvaluateCellular(float2 sample, uint seed)
        {
            int2 baseCell = (int2)math.floor(sample);
            float first = float.MaxValue;
            float second = float.MaxValue;
            float third = float.MaxValue;

            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int2 cell = baseCell + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, seed);
                    float2 delta = sample - feature;
                    float distance = FastMagnitudeApprox(delta);
                    if (distance < first)
                    {
                        third = second;
                        second = first;
                        first = distance;
                    }
                    else if (distance < second)
                    {
                        third = second;
                        second = distance;
                    }
                    else if (distance < third)
                    {
                        third = distance;
                    }
                }
            }

            return new CellularData
            {
                F1 = first,
                F2 = second,
                F3 = third
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastMagnitudeApprox(float2 value)
        {
            float2 abs = math.abs(value);
            float max = math.max(abs.x, abs.y);
            float min = math.min(abs.x, abs.y);
            return max + (min * 0.41421356f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 Hash2(int x, int y, uint seed)
        {
            return new float2(
                Hash01(x, y, seed),
                Hash01(x, y, seed ^ 0x9E3779B9u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FractalValueNoise(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < 4; octave++)
            {
                total += ValueNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }

            return total / math.max(0.0001f, normalization);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ValueNoise(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * (3f - 2f * local);

            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);

            return math.lerp(
                math.lerp(a, b, smooth.x),
                math.lerp(c, d, smooth.x),
                smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int x, int y, uint seed)
        {
            uint hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
