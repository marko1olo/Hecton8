using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public struct SpaceEngine098RidgedMultifractalParams
    {
        public float Frequency;
        public float Strength01;
        public float Gain;
        public float Warp;
        public float FirstOctaveValue;
        public float Lacunarity;
        public float H;
        public float Offset;
        public float RidgeSmooth;
        public int Octaves;
    }

    public struct SpaceEngine098CraterProfile
    {
        public float RadPeak;
        public float RadInner;
        public float RadRim;
        public float RadOuter;
        public float HeightFloor;
        public float HeightPeak;
        public float HeightRim;
        public float Distortion;

        public static SpaceEngine098CraterProfile OldDefault()
        {
            return new SpaceEngine098CraterProfile
            {
                RadPeak = 0.03f,
                RadInner = 0.15f,
                RadRim = 0.2f,
                RadOuter = 0.8f,
                HeightFloor = -0.1f,
                HeightPeak = 0.6f,
                HeightRim = 1f,
                Distortion = 1f
            };
        }
    }

    public struct SpaceEngine098RilleParams
    {
        public float CellFrequency;
        public float Depth01;
        public float Narrowness;
        public float Sharpness;
        public float DomainWarpMeters;
        public float DomainWarpFrequency;
        public float RimLift01;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public static class SpaceEngine098TerrainMath
    {
        public const float DefaultLacunarity = 2.21828f;
        public const float DefaultH = 0.5f;
        public const float DefaultOffset = 0.8f;
        public const float DefaultRidgeSmooth = 0.0001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = math.saturate((x - edge0) / math.max(1e-6f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RidgedMultifractalErodedDetail(
            float3 point,
            in SpaceEngine098RidgedMultifractalParams parameters,
            uint seed)
        {
            int octaves = math.clamp(parameters.Octaves, 2, 12);
            float lacunarity = math.max(1.0001f, parameters.Lacunarity);
            float h = math.max(0.0001f, parameters.H);
            float frequency = lacunarity;
            float amplitude = math.pow(lacunarity, -h);
            float signal = math.max(0f, parameters.FirstOctaveValue);
            float sum = 0f;
            float3 dsum = default;

            for (int i = 1; i < octaves; i++)
            {
                float4 noiseDeriv = ValueNoise3DWithDerivative(
                    (point + parameters.Warp * dsum) * frequency,
                    seed + (uint)i * 4099u);
                float weight = math.saturate(signal * math.max(0f, parameters.Gain));
                signal = parameters.Offset - math.sqrt(math.max(0f, parameters.RidgeSmooth) + noiseDeriv.w * noiseDeriv.w);
                signal *= signal * weight;
                sum += signal * amplitude;
                dsum -= amplitude * noiseDeriv.xyz * noiseDeriv.w;
                frequency *= lacunarity;
                amplitude *= math.pow(lacunarity, -h);
            }

            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CraterHeightFuncSE(
            float lastlastLand,
            float lastLand,
            float height,
            float r,
            in SpaceEngine098CraterProfile crater)
        {
            float distHeight = crater.Distortion * height;

            float t = 1f - r / math.max(1e-5f, crater.RadPeak);
            float peak = crater.HeightPeak * crater.Distortion * SmoothStep(0f, 1f, t);

            t = SmoothStep(0f, 1f, (r - crater.RadInner) / math.max(1e-5f, crater.RadRim - crater.RadInner));
            float inoutMask = t * t * t;
            float innerRim = crater.HeightRim * distHeight * SmoothStep(0f, 1f, inoutMask);

            t = SmoothStep(0f, 1f, (crater.RadOuter - r) / math.max(1e-5f, crater.RadOuter - crater.RadRim));
            float outerRim = distHeight * math.lerp(0.05f, crater.HeightRim, t * t);

            t = math.saturate((1f - r) / math.max(1e-5f, 1f - crater.RadOuter));
            float halo = 0.05f * distHeight * t;

            float inside = lastlastLand + height * crater.HeightFloor + peak + innerRim;
            float outside = lastLand + outerRim + halo;
            return math.lerp(inside, outside, inoutMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Cell2F1F2(float2 point, uint seed)
        {
            int2 baseCell = (int2)math.floor(point);
            float f1 = float.MaxValue;
            float f2 = float.MaxValue;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = baseCell + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell, seed);
                    float distSq = math.lengthsq(feature - point);
                    if (distSq < f1)
                    {
                        f2 = f1;
                        f1 = distSq;
                    }
                    else if (distSq < f2)
                    {
                        f2 = distSq;
                    }
                }
            }

            return math.sqrt(new float2(f1, f2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DomainWarp2(float2 absoluteXZ, float amplitudeMeters, float frequency, uint seed)
        {
            float amplitude = math.max(0f, amplitudeMeters);
            if (amplitude <= 0.0001f)
                return float2.zero;

            float safeFrequency = math.max(0.0000001f, frequency);
            float2 sample = absoluteXZ * safeFrequency;
            float warpX = Fbm2(sample, 4, 2.03f, 0.5f, seed ^ 0x5F356495u) * 2f - 1f;
            float warpZ = Fbm2(sample + new float2(19.17f, -43.71f), 4, 2.03f, 0.5f, seed ^ 0xC2B2AE35u) * 2f - 1f;
            return new float2(warpX, warpZ) * amplitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fbm2(float2 point, int octaves, float lacunarity, float h, uint seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float normalization = 0f;
            float frequency = 1f;
            float gain = math.pow(math.max(1.0001f, lacunarity), -math.max(0.0001f, h));
            int count = math.clamp(octaves, 1, 8);

            for (int i = 0; i < count; i++)
            {
                sum += ValueNoise2D(point * frequency, seed + (uint)i * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum / math.max(0.0001f, normalization);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixSeed(uint seed, int x, int z)
        {
            uint hash = seed;
            hash ^= (uint)x * 0x8DA6B343u;
            hash ^= (uint)z * 0xD8163841u;
            return Avalanche(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Avalanche(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Float01(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 ValueNoise3DWithDerivative(float3 point, uint seed)
        {
            float3 floorPoint = math.floor(point);
            int3 cell = (int3)floorPoint;
            float3 f = point - floorPoint;
            float3 u = f * f * (3f - 2f * f);
            float3 du = 6f * f * (1f - f);

            float n000 = Hash01(cell + new int3(0, 0, 0), seed);
            float n100 = Hash01(cell + new int3(1, 0, 0), seed);
            float n010 = Hash01(cell + new int3(0, 1, 0), seed);
            float n110 = Hash01(cell + new int3(1, 1, 0), seed);
            float n001 = Hash01(cell + new int3(0, 0, 1), seed);
            float n101 = Hash01(cell + new int3(1, 0, 1), seed);
            float n011 = Hash01(cell + new int3(0, 1, 1), seed);
            float n111 = Hash01(cell + new int3(1, 1, 1), seed);

            float nx00 = math.lerp(n000, n100, u.x);
            float nx10 = math.lerp(n010, n110, u.x);
            float nx01 = math.lerp(n001, n101, u.x);
            float nx11 = math.lerp(n011, n111, u.x);
            float nxy0 = math.lerp(nx00, nx10, u.y);
            float nxy1 = math.lerp(nx01, nx11, u.y);
            float value = math.lerp(nxy0, nxy1, u.z);

            float dx0 = math.lerp(n100 - n000, n110 - n010, u.y);
            float dx1 = math.lerp(n101 - n001, n111 - n011, u.y);
            float dnx = math.lerp(dx0, dx1, u.z) * du.x;

            float dy0 = math.lerp(n010 - n000, n110 - n100, u.x);
            float dy1 = math.lerp(n011 - n001, n111 - n101, u.x);
            float dny = math.lerp(dy0, dy1, u.z) * du.y;

            float dnz = (nxy1 - nxy0) * du.z;
            return new float4(new float3(dnx, dny, dnz) * 2f, value * 2f - 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ValueNoise2D(float2 point, uint seed)
        {
            float2 floorPoint = math.floor(point);
            int2 cell = (int2)floorPoint;
            float2 f = point - floorPoint;
            float2 u = f * f * (3f - 2f * f);

            float a = Hash01(cell + new int2(0, 0), seed);
            float b = Hash01(cell + new int2(1, 0), seed);
            float c = Hash01(cell + new int2(0, 1), seed);
            float d = Hash01(cell + new int2(1, 1), seed);
            return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 Hash2(int2 cell, uint seed)
        {
            uint h = MixSeed(seed, cell.x, cell.y);
            return new float2(
                Float01(h),
                Float01(Avalanche(h ^ 0x9E3779B9u)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int2 cell, uint seed)
        {
            return Float01(MixSeed(seed, cell.x, cell.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int3 cell, uint seed)
        {
            uint hash = seed;
            hash ^= (uint)cell.x * 0x8DA6B343u;
            hash ^= (uint)cell.y * 0xD8163841u;
            hash ^= (uint)cell.z * 0xCB1AB31Fu;
            return Float01(Avalanche(hash));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098RidgedMultifractalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public SpaceEngine098RidgedMultifractalParams Parameters;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            double2 absolute = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
            float3 point = new float3((float)absolute.x, 0f, (float)absolute.y) * math.max(0.0000001f, Parameters.Frequency);
            float ridged = SpaceEngine098TerrainMath.RidgedMultifractalErodedDetail(point, in Parameters, Seed);
            OutputHeights01[index] = math.saturate(InputHeights01[index] + ridged * math.saturate(Parameters.Strength01));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098CraterPlacementJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float3> CraterAupCenters;
        public double2 WorldOriginXZ;
        public double2 WorldSizeXZ;
        public float RadiusMeters;
        public uint Seed;

        public void Execute(int index)
        {
            int originCellX = (int)math.floor(WorldOriginXZ.x / math.max(1f, RadiusMeters));
            int originCellZ = (int)math.floor(WorldOriginXZ.y / math.max(1f, RadiusMeters));
            uint h = SpaceEngine098TerrainMath.MixSeed(Seed + (uint)index * 0x9E3779B9u, originCellX, originCellZ);
            float x = (float)(WorldOriginXZ.x + SpaceEngine098TerrainMath.Float01(h) * WorldSizeXZ.x);
            float z = (float)(WorldOriginXZ.y + SpaceEngine098TerrainMath.Float01(SpaceEngine098TerrainMath.Avalanche(h ^ 0xB5297A4Du)) * WorldSizeXZ.y);
            CraterAupCenters[index] = new float3(x, 0f, z);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098ApplyCraterHeightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        [ReadOnly] public NativeArray<float3> CraterAupCenters;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public float RadiusMeters;
        public float Amplitude01;
        public SpaceEngine098CraterProfile Profile;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            double2 absoluteDouble = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
            float2 absolute = new float2((float)absoluteDouble.x, (float)absoluteDouble.y);
            float height = math.saturate(InputHeights01[index]);
            float deformation = 0f;
            float safeRadius = math.max(0.001f, RadiusMeters);

            for (int i = 0; i < CraterAupCenters.Length; i++)
            {
                float3 center = CraterAupCenters[i];
                float r = math.length(absolute - new float2(center.x, center.z)) / safeRadius;
                if (r >= 1f)
                    continue;

                deformation += SpaceEngine098TerrainMath.CraterHeightFuncSE(
                    0f,
                    0f,
                    Amplitude01,
                    r,
                    in Profile);
            }

            OutputHeights01[index] = math.saturate(height + deformation);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098RilleFissureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputHeights01;
        [WriteOnly] public NativeArray<float> OutputHeights01;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public SpaceEngine098RilleParams Parameters;
        public uint Seed;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            double2 absoluteDouble = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
            float2 absolute = new float2((float)absoluteDouble.x, (float)absoluteDouble.y);
            float2 warped = absolute + SpaceEngine098TerrainMath.DomainWarp2(
                absolute,
                Parameters.DomainWarpMeters,
                Parameters.DomainWarpFrequency,
                Seed);
            float2 cell = SpaceEngine098TerrainMath.Cell2F1F2(warped * math.max(0.0000001f, Parameters.CellFrequency), Seed);
            float borderDistance = math.abs(cell.y - cell.x);
            float r = SpaceEngine098TerrainMath.SmoothStep(0f, 1f, math.max(1f, Parameters.Narrowness) * borderDistance);
            float fissure = math.pow(1f - r, math.max(0.25f, Parameters.Sharpness));
            float shoulder = SpaceEngine098TerrainMath.SmoothStep(0.35f, 0.8f, r) * (1f - r) * math.max(0f, Parameters.RimLift01);
            float height = math.saturate(InputHeights01[index]);
            OutputHeights01[index] = math.saturate(height - fissure * math.max(0f, Parameters.Depth01) + shoulder);
        }
    }
}
