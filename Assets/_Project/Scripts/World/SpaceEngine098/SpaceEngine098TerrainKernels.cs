using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct SpaceEngine098RidgedMultifractalParams
    {
        [FieldOffset(0)] public float Frequency;
        [FieldOffset(4)] public float Strength01;
        [FieldOffset(8)] public float Gain;
        [FieldOffset(12)] public float Warp;
        [FieldOffset(16)] public float FirstOctaveValue;
        [FieldOffset(20)] public float Lacunarity;
        [FieldOffset(24)] public float H;
        [FieldOffset(28)] public float Offset;
        [FieldOffset(32)] public float RidgeSmooth;
        [FieldOffset(36)] public int Octaves;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpaceEngine098CraterProfile
    {
        [FieldOffset(0)] public float RadPeak;
        [FieldOffset(4)] public float RadInner;
        [FieldOffset(8)] public float RadRim;
        [FieldOffset(12)] public float RadOuter;
        [FieldOffset(16)] public float HeightFloor;
        [FieldOffset(20)] public float HeightPeak;
        [FieldOffset(24)] public float HeightRim;
        [FieldOffset(28)] public float Distortion;

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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpaceEngine098RilleParams
    {
        [FieldOffset(0)] public float CellFrequency;
        [FieldOffset(4)] public float Depth01;
        [FieldOffset(8)] public float Narrowness;
        [FieldOffset(12)] public float Sharpness;
        [FieldOffset(16)] public float DomainWarpMeters;
        [FieldOffset(20)] public float DomainWarpFrequency;
        [FieldOffset(24)] public float RimLift01;
        [FieldOffset(28)] private uint _reserved0;
    }

    /// <summary>
    /// Per-sample terrain pipeline audit record produced by the Burst metrics pass.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpaceEngine098PipelineMetricSample
    {
        [FieldOffset(0)] public float MinHeight;
        [FieldOffset(4)] public float MaxHeight;
        [FieldOffset(8)] public float RidgedDelta;
        [FieldOffset(12)] public float CraterDelta;
        [FieldOffset(16)] public float RilleDelta;
        [FieldOffset(20)] public int ChecksumContribution;
        [FieldOffset(24)] public byte IsFinite;
        [FieldOffset(25)] public byte HasChecksumContribution;
        [FieldOffset(26)] private ushort _reserved0;
        [FieldOffset(28)] private uint _reserved1;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public static class SpaceEngine098TerrainMath
    {
        public const float DefaultLacunarity = 2.218281828459f;
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
        public static float ApproxSpectralGain(float lacunarity, float h)
        {
            float safeLacunarity = math.clamp(math.select(DefaultLacunarity, lacunarity, math.isfinite(lacunarity)), 1.0001f, 8f);
            float safeH = math.clamp(math.select(DefaultH, h, math.isfinite(h)), 0.0001f, 4f);
            float x = safeH * (safeLacunarity - 1f);
            float denominator = 1f + (0.66f * x) + (0.17f * x * x);
            float gain = math.rcp(math.max(0.0001f, denominator));
            return math.saturate(math.select(0.5f, gain, math.isfinite(gain)));
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
            float spectralGain = ApproxSpectralGain(lacunarity, h);
            float frequency = lacunarity;
            float amplitude = spectralGain;
            float signal = math.max(0f, parameters.FirstOctaveValue);
            float sum = 0f;
            float3 dsum = default;

            for (int i = 1; i < octaves; i++)
            {
                float4 noiseDeriv = ValueNoise3DWithDerivative(
                    (point + parameters.Warp * dsum) * frequency,
                    seed + (uint)i * 4099u);
                float weight = math.saturate(signal * math.max(0f, parameters.Gain));
                signal = parameters.Offset - FastRidgeMagnitude(noiseDeriv.w, math.max(0f, parameters.RidgeSmooth));
                signal *= signal * weight;
                sum += signal * amplitude;
                dsum -= amplitude * noiseDeriv.xyz * noiseDeriv.w;
                frequency *= lacunarity;
                amplitude *= spectralGain;
            }

            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DowncastProceduralPhase(double3 phase, float3 fallback)
        {
            if (!math.all(math.isfinite(phase)))
                return fallback;

            float3 result = new float3((float)phase.x, (float)phase.y, (float)phase.z);
            return math.all(math.isfinite(result)) ? result : fallback;
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
                    float distance = FastMagnitudeApprox(feature - point);
                    if (distance < f1)
                    {
                        f2 = f1;
                        f1 = distance;
                    }
                    else if (distance < f2)
                    {
                        f2 = distance;
                    }
                }
            }

            return new float2(f1, f2);
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
            float gain = ApproxSpectralGain(math.max(1.0001f, lacunarity), math.max(0.0001f, h));
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
        public static float FastDistance2(float2 value)
        {
            return FastMagnitudeApprox(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastRidgeMagnitude(float signal, float smooth)
        {
            return math.abs(signal) + (smooth * 0.5f);
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

    /// <summary>
    /// Literal SpaceEngine 0.9.8 noise utility facade matching the research report naming.
    /// Existing jobs use <see cref="SpaceEngine098TerrainMath"/> directly; this type keeps integration code aligned with the extracted equations.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public static class SpaceEngineNoise098
    {
        public const float DefaultLacunarity = SpaceEngine098TerrainMath.DefaultLacunarity;
        public const float DefaultH = SpaceEngine098TerrainMath.DefaultH;
        public const float DefaultOffset = SpaceEngine098TerrainMath.DefaultOffset;
        public const float DefaultRidgeSmooth = SpaceEngine098TerrainMath.DefaultRidgeSmooth;

        /// <summary>
        /// Saturates a scalar to the [0,1] interval.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Saturate(float value)
        {
            return math.saturate(value);
        }

        /// <summary>
        /// SpaceEngine-style cubic smoothstep.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float edge0, float edge1, float value)
        {
            return SpaceEngine098TerrainMath.SmoothStep(edge0, edge1, value);
        }

        /// <summary>
        /// Deterministic seed mix used for AUP chunk anchoring.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixSeed(uint seed, int x, int z)
        {
            return SpaceEngine098TerrainMath.MixSeed(seed, x, z);
        }

        /// <summary>
        /// Deterministic uint avalanche hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Avalanche(uint hash)
        {
            return SpaceEngine098TerrainMath.Avalanche(hash);
        }

        /// <summary>
        /// Converts a hash to a deterministic [0,1] scalar.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Float01(uint hash)
        {
            return SpaceEngine098TerrainMath.Float01(hash);
        }

        /// <summary>
        /// Returns deterministic 2D cellular F1/F2 distances for rille and crack fields.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Cell2F1F2(float2 point, uint seed)
        {
            return SpaceEngine098TerrainMath.Cell2F1F2(point, seed);
        }

        /// <summary>
        /// Returns deterministic fBM domain warp in meters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DomainWarp2(float2 absoluteXZ, float amplitudeMeters, float frequency, uint seed)
        {
            return SpaceEngine098TerrainMath.DomainWarp2(absoluteXZ, amplitudeMeters, frequency, seed);
        }

        /// <summary>
        /// Returns deterministic 2D fBM used by the SpaceEngine rille domain warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Fbm2(float2 point, int octaves, float lacunarity, float h, uint seed)
        {
            return SpaceEngine098TerrainMath.Fbm2(point, octaves, lacunarity, h, seed);
        }

        /// <summary>
        /// SpaceEngine ridged multifractal eroded-detail kernel using the default extracted constants.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RidgedMultifractalErodedDetail(float3 point, in SpaceEngine098RidgedMultifractalParams parameters, uint seed)
        {
            return SpaceEngine098TerrainMath.RidgedMultifractalErodedDetail(point, in parameters, seed);
        }

        /// <summary>
        /// SpaceEngine ridged multifractal eroded-detail overload matching the research report scaffold.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RidgedMultifractalErodedDetail(
            float3 point,
            int octaves,
            float gain,
            float warp,
            float firstOctaveValue,
            float lacunarity,
            float h,
            float offset,
            float ridgeSmooth,
            uint seed)
        {
            var parameters = new SpaceEngine098RidgedMultifractalParams
            {
                Frequency = 1f,
                Strength01 = 1f,
                Gain = gain,
                Warp = warp,
                FirstOctaveValue = firstOctaveValue,
                Lacunarity = lacunarity,
                H = h,
                Offset = offset,
                RidgeSmooth = ridgeSmooth,
                Octaves = octaves
            };
            return SpaceEngine098TerrainMath.RidgedMultifractalErodedDetail(point, in parameters, seed);
        }

        /// <summary>
        /// SpaceEngine analytic crater profile facade.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CraterHeightFuncSE(
            float lastlastLand,
            float lastLand,
            float height,
            float r,
            in SpaceEngine098CraterProfile crater)
        {
            return SpaceEngine098TerrainMath.CraterHeightFuncSE(lastlastLand, lastLand, height, r, in crater);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098RidgedMultifractalJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public SpaceEngine098RidgedMultifractalParams Parameters;
        public uint Seed;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int x = index % safeWidth;
            int z = index / safeWidth;
            double2 sample = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
            double safeFrequency = math.max(0.0000001d, (double)Parameters.Frequency);
            float3 point = SpaceEngine098TerrainMath.DowncastProceduralPhase(
                new double3(sample.x * safeFrequency, 0d, sample.y * safeFrequency),
                float3.zero);
            float ridged = SpaceEngine098TerrainMath.RidgedMultifractalErodedDetail(point, in Parameters, Seed);
            OutputHeights01[index] = math.saturate(InputHeights01[index] + ridged * math.saturate(Parameters.Strength01));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098CraterPlacementJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<float3> CraterAupCenters;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098ApplyCraterHeightJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        [ReadOnly, NoAlias] public NativeArray<float3> CraterAupCenters;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public float RadiusMeters;
        public float Amplitude01;
        public SpaceEngine098CraterProfile Profile;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int x = index % safeWidth;
            int z = index / safeWidth;
            double2 absoluteDouble = WorldOriginXZ + new double2(x, z) * math.max(0.001, CellSizeMeters);
            float2 absolute = new float2((float)absoluteDouble.x, (float)absoluteDouble.y);
            float height = math.saturate(InputHeights01[index]);
            float deformation = 0f;
            float safeRadius = math.max(0.001f, RadiusMeters);

            for (int i = 0; i < CraterAupCenters.Length; i++)
            {
                float3 center = CraterAupCenters[i];
                float r = SpaceEngine098TerrainMath.FastDistance2(absolute - new float2(center.x, center.z)) / safeRadius;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098RilleFissureJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [WriteOnly, NoAlias] public NativeArray<float> OutputHeights01;
        public int Width;
        public double2 WorldOriginXZ;
        public double CellSizeMeters;
        public SpaceEngine098RilleParams Parameters;
        public uint Seed;

        public void Execute(int index)
        {
            int safeWidth = math.max(1, Width);
            int x = index % safeWidth;
            int z = index / safeWidth;
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
            float fissure = SpaceEngine098TerrainMath.ApproxPow01Curve(1f - r, math.max(0.25f, Parameters.Sharpness));
            float shoulder = SpaceEngine098TerrainMath.SmoothStep(0.35f, 0.8f, r) * (1f - r) * math.max(0f, Parameters.RimLift01);
            float height = math.saturate(InputHeights01[index]);
            OutputHeights01[index] = math.saturate(height - fissure * math.max(0f, Parameters.Depth01) + shoulder);
        }
    }

    /// <summary>
    /// Computes per-sample validation metrics for the terrain pipeline without managed float-array reads.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SpaceEngine098PipelineMetricsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> InputHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> RidgedHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> CraterHeights01;
        [ReadOnly, NoAlias] public NativeArray<float> RilleHeights01;
        [WriteOnly, NoAlias] public NativeArray<SpaceEngine098PipelineMetricSample> Metrics;
        public int ChecksumStride;

        public void Execute(int index)
        {
            float inputHeight = InputHeights01[index];
            float ridgedHeight = RidgedHeights01[index];
            float craterHeight = CraterHeights01[index];
            float rilleHeight = RilleHeights01[index];
            bool contributesChecksum = index % math.max(1, ChecksumStride) == 0;

            Metrics[index] = new SpaceEngine098PipelineMetricSample
            {
                MinHeight = rilleHeight,
                MaxHeight = rilleHeight,
                RidgedDelta = math.abs(ridgedHeight - inputHeight),
                CraterDelta = math.abs(craterHeight - ridgedHeight),
                RilleDelta = math.abs(rilleHeight - craterHeight),
                ChecksumContribution = contributesChecksum ? (int)math.round(rilleHeight * 100000f) : 0,
                IsFinite = math.isfinite(inputHeight) &&
                           math.isfinite(ridgedHeight) &&
                           math.isfinite(craterHeight) &&
                           math.isfinite(rilleHeight) ? (byte)1 : (byte)0,
                HasChecksumContribution = contributesChecksum ? (byte)1 : (byte)0
            };
        }
    }
}
