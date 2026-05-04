using System.Runtime.CompilerServices;
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

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            float source = math.saturate(InputHeights01[index]);
            float2 world = WorldOriginXZ + new float2(x, z) * math.max(0.001f, CellSizeMeters);
            float2 sample = world * math.max(0.0001f, Frequency);

            float ridgeNoise = FractalValueNoise(sample, Seed);
            float ridge = 1f - math.abs(ridgeNoise * 2f - 1f);
            float sharpenedRidge = math.pow(math.saturate(ridge), math.max(0.5f, RidgeSharpness));

            float slabNoise = FractalValueNoise(sample * 0.47f + new float2(37.13f, -19.71f), Seed ^ 0x9E3779B9u);
            float fracture = math.smoothstep(0.36f, 0.92f, math.abs(slabNoise * 2f - 1f));
            float ridgeMask = math.smoothstep(0.24f, 0.82f, sharpenedRidge);
            float extrusion = ridgeMask * math.lerp(0.62f, 1.18f, fracture) * math.saturate(Strength01);

            OutputHeights01[index] = math.saturate(source + extrusion);
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
