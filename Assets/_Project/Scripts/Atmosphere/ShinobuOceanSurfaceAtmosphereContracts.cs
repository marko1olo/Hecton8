using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    public static class OceanSurfaceAtmosphereConstants
    {
        public const int WaveCapacity = 16;
        public const int MinQualityWaveCount = 4;
        public const int TelemetryFrameCount = 300;
        public const int MockBuoyancyQueryCount = 10000;
        public const long MockBuoyancyBudgetNs = 100000L;
        public const long TelemetryDumpBudgetNs = 500000L;
        public const uint SourceHash = 0x53485236u; // SHR6
        public const uint WaterlineBreachLaneHash = 0x57425236u; // WBR6
        public const float DefaultSeaLevel = 0f;
        public const float MinimumWavelength = 0.25f;
        public const float TwoPi = 6.2831853071795864769f;
        public const ulong SeedShipActivatedNarrativeMask = 1UL << 61;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WaveParametersDTO
    {
        [FieldOffset(0)] public float4 DirectionAndSteepness;
        [FieldOffset(16)] public float PhaseSpeed;
        [FieldOffset(20)] public float Amplitude;
        [FieldOffset(24)] public float Wavelength;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AtmosphereDTO
    {
        [FieldOffset(0)] public float4 RayleighBeta;
        [FieldOffset(16)] public float4 MieBeta;
        [FieldOffset(32)] public float4 ScatteringParams;
        [FieldOffset(48)] public float4 PlanetParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WeatherStateDTO
    {
        [FieldOffset(0)] public float4 WindDirectionSpeedStorm;
        [FieldOffset(16)] public float4 SurfaceScalars;
        [FieldOffset(32)] public float4 SkyTintAndSurge;
        [FieldOffset(48)] public uint StateMask;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public float MaxWaveAmplitude;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanSurfaceLodDTO
    {
        [FieldOffset(0)] public float4 CameraAupLocalXZ;
        [FieldOffset(16)] public float4 GridParams;
        [FieldOffset(32)] public float4 RingParams;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public float HorizonMeters;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanSurfaceTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float MaxWaveHeight;
        [FieldOffset(12)] public float StormIntensity;
        [FieldOffset(16)] public long WaveComputeTimeNs;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public int ActiveWaveCount;
        [FieldOffset(32)] public float SurfaceDisturbance;
        [FieldOffset(36)] public float FoamScalar;
        [FieldOffset(40)] public float3 LastNormal;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockBuoyancyQuery
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float TimeSeconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint Seed;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float SeaLevel;
        [FieldOffset(44)] public float _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockBuoyancyResult
    {
        [FieldOffset(0)] public float Height;
        [FieldOffset(4)] public float3 Normal;
        [FieldOffset(16)] public float3 Displacement;
        [FieldOffset(28)] public uint Flags;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public static class HectonOceanSurfaceMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDesiredWaveCount(float qualityWeight, int maxWaveCount)
        {
            int maxCount = math.clamp(maxWaveCount, 1, OceanSurfaceAtmosphereConstants.WaveCapacity);
            float q = math.saturate(qualityWeight);
            float normalized = math.saturate((q - 0.1f) * (1f / 0.9f));
            float qualityCurve = normalized * normalized * (3f - (2f * normalized));
            qualityCurve *= math.step(0.1f, q);
            float minimum = math.min(OceanSurfaceAtmosphereConstants.MinQualityWaveCount, maxCount);
            return math.min(maxCount, math.lerp(minimum, maxCount, qualityCurve));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveFullWaveCount(float qualityWeight, int maxWaveCount)
        {
            return math.clamp((int)math.floor(ResolveDesiredWaveCount(qualityWeight, maxWaveCount)), 1, math.max(1, maxWaveCount));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWaveContribution(int waveIndex, float qualityWeight, int maxWaveCount)
        {
            return math.saturate(ResolveDesiredWaveCount(qualityWeight, maxWaveCount) - waveIndex);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static void EvaluateWaves(
            double3 AUP,
            float time,
            NativeArray<WaveParametersDTO> waves,
            out float height,
            out float3 normal)
        {
            EvaluateWaves(AUP, time, waves, 1f, out height, out normal);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static void EvaluateWaves(
            double3 AUP,
            float time,
            NativeArray<WaveParametersDTO> waves,
            float globalQualityWeight,
            out float height,
            out float3 normal)
        {
            EvaluateWavesDetailed(
                AUP,
                time,
                waves,
                globalQualityWeight,
                out height,
                out normal,
                out _,
                out _,
                out _);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static void EvaluateWavesDetailed(
            double3 AUP,
            float time,
            NativeArray<WaveParametersDTO> waves,
            float globalQualityWeight,
            out float height,
            out float3 normal,
            out float3 displacement,
            out float jacobianDeterminant,
            out float activeWaveWeight)
        {
            height = 0f;
            normal = new float3(0f, 1f, 0f);
            displacement = float3.zero;
            jacobianDeterminant = 1f;
            activeWaveWeight = 0f;

            if (!waves.IsCreated || waves.Length <= 0 || !math.all(math.isfinite(AUP)) || !math.isfinite(time))
                return;

            int waveLimit = math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity);
            float dHeightDx = 0f;
            float dHeightDz = 0f;
            float minJacobian = 1f;

            for (int i = 0; i < waveLimit; i++)
            {
                float contribution = ResolveWaveContribution(i, globalQualityWeight, waveLimit);
                if (contribution <= 0.0001f)
                    continue;

                WaveParametersDTO wave = waves[i];
                float amplitude = FiniteNonNegative(wave.Amplitude);
                float wavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, FinitePositive(wave.Wavelength, 24f));
                if (amplitude <= 0.000001f)
                    continue;

                float2 direction = Normalize2OrDefault(wave.DirectionAndSteepness.xy, new float2(1f, 0f));
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / wavelength;
                double projected = (AUP.x * direction.x) + (AUP.z * direction.y);
                double wrappedMeters = WrapMeters(projected, wavelength);
                float phase = WrapPhaseRadians((float)(wrappedMeters * waveNumber) + wave.DirectionAndSteepness.z + (wave.PhaseSpeed * time));

                math.sincos(phase, out float sine, out float cosine);

                float weightedAmplitude = amplitude * contribution;
                height += weightedAmplitude * sine;
                activeWaveWeight += contribution;

                float slope = weightedAmplitude * waveNumber * cosine;
                dHeightDx += slope * direction.x;
                dHeightDz += slope * direction.y;

                float steepness = math.saturate(wave.DirectionAndSteepness.w);
                float horizontal = steepness * weightedAmplitude * cosine;
                displacement.x += direction.x * horizontal;
                displacement.y += weightedAmplitude * sine;
                displacement.z += direction.y * horizontal;

                float jacobian = 1f - (steepness * weightedAmplitude * waveNumber * sine);
                minJacobian = math.min(minJacobian, jacobian);
            }

            float3 rawNormal = new float3(-dHeightDx, 1f, -dHeightDz);
            float normalLenSq = math.max(0.000001f, math.dot(rawNormal, rawNormal));
            rawNormal *= math.rsqrt(normalLenSq);

            normal = math.all(math.isfinite(rawNormal)) ? rawNormal : new float3(0f, 1f, 0f);
            jacobianDeterminant = math.isfinite(minJacobian) ? minJacobian : 1f;
            if (!math.isfinite(height))
                height = 0f;
            if (!math.all(math.isfinite(displacement)))
                displacement = float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveFoamScalar(float jacobianDeterminant, float foamThreshold, float globalQualityWeight)
        {
            float qualityFoam = math.saturate((globalQualityWeight - 0.28f) * (1f / 0.72f));
            qualityFoam *= math.step(0.28f, math.saturate(globalQualityWeight));
            float pinched = math.saturate((foamThreshold - jacobianDeterminant) * 4f);
            return pinched * qualityFoam;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanSurfaceLodDTO ResolveRadialGridLod(double3 cameraAup, float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            OceanSurfaceLodDTO dto = default;
            dto.CameraAupLocalXZ = new float4((float)WrapMeters(cameraAup.x, 4096f), (float)WrapMeters(cameraAup.z, 4096f), 0f, 0f);
            dto.GridParams = new float4(math.lerp(12f, 48f, q), math.lerp(36f, 144f, q), math.lerp(18f, 7f, q), math.lerp(64f, 224f, q));
            dto.RingParams = new float4(math.lerp(4f, 9f, q), math.lerp(1.85f, 1.38f, q), math.lerp(512f, 4096f, q), 0f);
            dto.GlobalQualityWeight = q;
            dto.HorizonMeters = math.lerp(900f, 5200f, q);
            dto.Flags = (uint)math.step(0.28f, q);
            return dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashWaveState(NativeArray<WaveParametersDTO> waves, int count, float time, float quality)
        {
            uint hash = 2166136261u;
            int limit = math.min(math.min(count, waves.IsCreated ? waves.Length : 0), OceanSurfaceAtmosphereConstants.WaveCapacity);
            for (int i = 0; i < limit; i++)
            {
                WaveParametersDTO wave = waves[i];
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.x));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.y));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.z));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.w));
                hash = Hash(hash, math.asuint(wave.PhaseSpeed));
                hash = Hash(hash, math.asuint(wave.Amplitude));
                hash = Hash(hash, math.asuint(wave.Wavelength));
            }

            hash = Hash(hash, math.asuint(time));
            hash = Hash(hash, math.asuint(quality));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double WrapMeters(double value, double wavelength)
        {
            double safeWavelength = math.max(0.0001, math.abs(wavelength));
            double quotient = math.floor(value / safeWavelength);
            return value - (quotient * safeWavelength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapPhaseRadians(float phase)
        {
            float safePhase = math.isfinite(phase) ? phase : 0f;
            float quotient = math.floor(safePhase * (1f / OceanSurfaceAtmosphereConstants.TwoPi));
            return safePhase - (quotient * OceanSurfaceAtmosphereConstants.TwoPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Normalize2OrDefault(float2 value, float2 fallback)
        {
            float lenSq = math.dot(value, value);
            if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                return fallback;

            return value * math.rsqrt(lenSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WaveParametersDTO SanitizeWave(WaveParametersDTO wave)
        {
            wave.DirectionAndSteepness.xy = Normalize2OrDefault(wave.DirectionAndSteepness.xy, new float2(1f, 0f));
            wave.DirectionAndSteepness.z = WrapPhaseRadians(wave.DirectionAndSteepness.z);
            wave.DirectionAndSteepness.w = math.saturate(wave.DirectionAndSteepness.w);
            wave.PhaseSpeed = math.isfinite(wave.PhaseSpeed) ? wave.PhaseSpeed : 0f;
            wave.Amplitude = FiniteNonNegative(wave.Amplitude);
            wave.Wavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, FinitePositive(wave.Wavelength, 24f));
            wave._pad0 = 0u;
            return wave;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FinitePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegative(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint current, uint value)
        {
            current ^= value;
            return current * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct OceanBuoyancyHeightJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float3> RuntimePositions;
        [ReadOnly, NoAlias] public NativeArray<WaveParametersDTO> Waves;
        [NoAlias] public NativeArray<float> Heights;
        [NoAlias] public NativeArray<float3> Normals;
        public double3 RuntimeOriginAUP;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public float SeaLevel;

        public void Execute(int index)
        {
            float3 runtime = RuntimePositions[index];
            double3 aup = RuntimeOriginAUP + new double3(runtime.x, runtime.y, runtime.z);
            HectonOceanSurfaceMath.EvaluateWaves(aup, TimeSeconds, Waves, GlobalQualityWeight, out float relativeHeight, out float3 normal);
            Heights[index] = SeaLevel + relativeHeight;
            if (Normals.IsCreated && index < Normals.Length)
                Normals[index] = normal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct OceanBuoyancyAupJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<double3> AUPs;
        [ReadOnly, NoAlias] public NativeArray<WaveParametersDTO> Waves;
        [NoAlias] public NativeArray<float> Heights;
        [NoAlias] public NativeArray<float3> Normals;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public float SeaLevel;

        public void Execute(int index)
        {
            HectonOceanSurfaceMath.EvaluateWaves(AUPs[index], TimeSeconds, Waves, GlobalQualityWeight, out float relativeHeight, out float3 normal);
            Heights[index] = SeaLevel + relativeHeight;
            if (Normals.IsCreated && index < Normals.Length)
                Normals[index] = normal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockBuoyancyQueryJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<MockBuoyancyQuery> Queries;
        [ReadOnly, NoAlias] public NativeArray<WaveParametersDTO> Waves;
        [NoAlias] public NativeArray<MockBuoyancyResult> Results;

        public void Execute(int index)
        {
            MockBuoyancyQuery query = Queries[index];
            HectonOceanSurfaceMath.EvaluateWavesDetailed(
                query.AUP,
                query.TimeSeconds,
                Waves,
                query.GlobalQualityWeight,
                out float relativeHeight,
                out float3 normal,
                out float3 displacement,
                out float jacobian,
                out _);

            MockBuoyancyResult result = default;
            result.Height = query.SeaLevel + relativeHeight;
            result.Normal = normal;
            result.Displacement = displacement;
            result.Flags = math.isfinite(jacobian) ? 1u : 2u;
            Results[index] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockBuoyancyQueryHydrationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MockBuoyancyQuery> Queries;
        public double3 CenterAUP;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public float SeaLevel;
        public uint Seed;
        public uint SectorHash;
        public uint SimulationFrame;

        public void Execute(int index)
        {
            uint randomSeed =
                ((uint)index * 747796405u) ^
                Seed ^
                SectorHash ^
                (SimulationFrame * 2246822519u) ^
                2891336453u;
            if (randomSeed == 0u)
                randomSeed = 1u;
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(randomSeed);
            float x = random.NextFloat(-5000f, 5000f);
            float z = random.NextFloat(-5000f, 5000f);
            MockBuoyancyQuery query = default;
            query.AUP = CenterAUP + new double3(x, 0.0, z);
            query.TimeSeconds = TimeSeconds;
            query.GlobalQualityWeight = GlobalQualityWeight;
            query.SeaLevel = SeaLevel;
            query.Seed = random.state;
            query.Flags = 1u;
            Queries[index] = query;
        }
    }

    public static class OceanWeatherCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';
        private const byte Space = (byte)' ';
        private const byte Tab = (byte)'\t';

        private const uint KeyWaveAmplitude = 0xD22FEB76u;
        private const uint KeyWaveWavelength = 0x9D078764u;
        private const uint KeyWaveSteepness = 0x8A751A07u;
        private const uint KeyWavePhaseSpeed = 0x7A39269Cu;
        private const uint KeyWaveDirectionX = 0x80B6D6D9u;
        private const uint KeyWaveDirectionZ = 0x7EB6D3B3u;
        private const uint KeyWindX = 0x924B90D0u;
        private const uint KeyWindZ = 0x944B93F6u;
        private const uint KeyWindSpeed = 0x73151F23u;
        private const uint KeyFoamThreshold = 0x4AB835DCu;
        private const uint KeyRain = 0x89A3B5D3u;
        private const uint KeyStorm = 0xBFE7AF96u;
        private const uint KeyGasGiantGlow = 0xC2380B90u;
        private const uint KeyRayleighX = 0x868D0777u;
        private const uint KeyRayleighY = 0x858D05E4u;
        private const uint KeyRayleighZ = 0x888D0A9Du;
        private const uint KeyMieX = 0x08825D9Du;
        private const uint KeyMieY = 0x07825C0Au;
        private const uint KeyMieZ = 0x06825A77u;
        private const uint KeySurfaceDisturbance = 0x40E6FE8Fu;
        private const uint KeySeaLevel = 0x65E53181u;
        private const uint KeySurge = 0xAF130DF9u;

        public static bool TryApply(
            NativeArray<byte> csvBytes,
            int length,
            NativeArray<WaveParametersDTO> waves,
            NativeArray<WeatherStateDTO> weather,
            NativeArray<AtmosphereDTO> atmosphere)
        {
            if (!csvBytes.IsCreated || length <= 0)
                return false;

            int safeLength = math.min(length, csvBytes.Length);
            bool changed = false;
            int rowStart = 0;
            while (rowStart < safeLength)
            {
                int rowEnd = rowStart;
                while (rowEnd < safeLength && csvBytes[rowEnd] != LineFeed && csvBytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                int firstComma = FindByte(csvBytes, rowStart, rowEnd, Comma);
                if (firstComma > rowStart)
                {
                    int keyStart = TrimStart(csvBytes, rowStart, firstComma);
                    int keyEnd = TrimEnd(csvBytes, keyStart, firstComma);
                    uint keyHash = HashKey(csvBytes, keyStart, keyEnd);

                    int secondComma = FindByte(csvBytes, firstComma + 1, rowEnd, Comma);
                    int index = -1;
                    int valueStart;
                    int valueEnd;
                    if (secondComma > firstComma)
                    {
                        TryParseInt(csvBytes, firstComma + 1, secondComma, out index);
                        valueStart = secondComma + 1;
                        valueEnd = rowEnd;
                    }
                    else
                    {
                        valueStart = firstComma + 1;
                        valueEnd = rowEnd;
                    }

                    if (TryParseFloat(csvBytes, valueStart, valueEnd, out float value))
                        changed |= ApplyValue(keyHash, index, value, waves, weather, atmosphere);
                }

                rowStart = rowEnd + 1;
                while (rowStart < safeLength && (csvBytes[rowStart] == LineFeed || csvBytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool ApplyValue(
            uint keyHash,
            int index,
            float value,
            NativeArray<WaveParametersDTO> waves,
            NativeArray<WeatherStateDTO> weather,
            NativeArray<AtmosphereDTO> atmosphere)
        {
            bool changed = false;
            switch (keyHash)
            {
                case KeyWaveAmplitude:
                case KeyWaveWavelength:
                case KeyWaveSteepness:
                case KeyWavePhaseSpeed:
                case KeyWaveDirectionX:
                case KeyWaveDirectionZ:
                    changed = ApplyWaveValue(keyHash, index, value, waves);
                    break;
                case KeyWindX:
                case KeyWindZ:
                case KeyWindSpeed:
                case KeyFoamThreshold:
                case KeyRain:
                case KeyStorm:
                case KeySurfaceDisturbance:
                case KeySeaLevel:
                case KeySurge:
                    changed = ApplyWeatherValue(keyHash, value, weather);
                    break;
                case KeyGasGiantGlow:
                case KeyRayleighX:
                case KeyRayleighY:
                case KeyRayleighZ:
                case KeyMieX:
                case KeyMieY:
                case KeyMieZ:
                    changed = ApplyAtmosphereValue(keyHash, value, atmosphere);
                    break;
            }

            return changed;
        }

        private static bool ApplyWaveValue(uint keyHash, int index, float value, NativeArray<WaveParametersDTO> waves)
        {
            if (!waves.IsCreated || waves.Length <= 0 || !math.isfinite(value))
                return false;

            int start = index >= 0 ? math.min(index, waves.Length - 1) : 0;
            int end = index >= 0 ? start + 1 : waves.Length;
            for (int i = start; i < end; i++)
            {
                WaveParametersDTO wave = waves[i];
                switch (keyHash)
                {
                    case KeyWaveAmplitude:
                        wave.Amplitude = math.max(0f, value);
                        break;
                    case KeyWaveWavelength:
                        wave.Wavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, value);
                        break;
                    case KeyWaveSteepness:
                        wave.DirectionAndSteepness.w = math.saturate(value);
                        break;
                    case KeyWavePhaseSpeed:
                        wave.PhaseSpeed = value;
                        break;
                    case KeyWaveDirectionX:
                        wave.DirectionAndSteepness.x = value;
                        break;
                    case KeyWaveDirectionZ:
                        wave.DirectionAndSteepness.y = value;
                        break;
                }

                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            return true;
        }

        private static bool ApplyWeatherValue(uint keyHash, float value, NativeArray<WeatherStateDTO> weather)
        {
            if (!weather.IsCreated || weather.Length <= 0 || !math.isfinite(value))
                return false;

            WeatherStateDTO state = weather[0];
            switch (keyHash)
            {
                case KeyWindX:
                    state.WindDirectionSpeedStorm.x = value;
                    break;
                case KeyWindZ:
                    state.WindDirectionSpeedStorm.y = value;
                    break;
                case KeyWindSpeed:
                    state.WindDirectionSpeedStorm.z = math.max(0f, value);
                    break;
                case KeyStorm:
                    state.WindDirectionSpeedStorm.w = math.saturate(value);
                    break;
                case KeyFoamThreshold:
                    state.SurfaceScalars.z = math.saturate(value);
                    break;
                case KeyRain:
                    state.SurfaceScalars.w = math.saturate(value);
                    break;
                case KeySurfaceDisturbance:
                    state.SkyTintAndSurge.w = math.max(0f, value);
                    break;
                case KeySeaLevel:
                    state.SurfaceScalars.x = value;
                    break;
                case KeySurge:
                    state.SkyTintAndSurge.w = math.max(state.SkyTintAndSurge.w, value);
                    break;
            }

            float2 wind = HectonOceanSurfaceMath.Normalize2OrDefault(state.WindDirectionSpeedStorm.xy, new float2(1f, 0f));
            state.WindDirectionSpeedStorm.x = wind.x;
            state.WindDirectionSpeedStorm.y = wind.y;
            weather[0] = state;
            return true;
        }

        private static bool ApplyAtmosphereValue(uint keyHash, float value, NativeArray<AtmosphereDTO> atmosphere)
        {
            if (!atmosphere.IsCreated || atmosphere.Length <= 0 || !math.isfinite(value))
                return false;

            AtmosphereDTO dto = atmosphere[0];
            switch (keyHash)
            {
                case KeyGasGiantGlow:
                    dto.ScatteringParams.y = math.max(0f, value);
                    break;
                case KeyRayleighX:
                    dto.RayleighBeta.x = math.max(0f, value);
                    break;
                case KeyRayleighY:
                    dto.RayleighBeta.y = math.max(0f, value);
                    break;
                case KeyRayleighZ:
                    dto.RayleighBeta.z = math.max(0f, value);
                    break;
                case KeyMieX:
                    dto.MieBeta.x = math.max(0f, value);
                    break;
                case KeyMieY:
                    dto.MieBeta.y = math.max(0f, value);
                    break;
                case KeyMieZ:
                    dto.MieBeta.z = math.max(0f, value);
                    break;
            }

            atmosphere[0] = dto;
            return true;
        }

        private static int FindByte(NativeArray<byte> bytes, int start, int end, byte value)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == value)
                    return i;
            }

            return -1;
        }

        private static int TrimStart(NativeArray<byte> bytes, int start, int end)
        {
            while (start < end && (bytes[start] == Space || bytes[start] == Tab))
                start++;
            return start;
        }

        private static int TrimEnd(NativeArray<byte> bytes, int start, int end)
        {
            while (end > start && (bytes[end - 1] == Space || bytes[end - 1] == Tab))
                end--;
            return end;
        }

        private static uint HashKey(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseInt(NativeArray<byte> bytes, int start, int end, out int value)
        {
            value = 0;
            start = TrimStart(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            if (start >= end)
                return false;

            int sign = 1;
            if (bytes[start] == (byte)'-')
            {
                sign = -1;
                start++;
            }

            int parsed = 0;
            bool any = false;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                parsed = (parsed * 10) + (c - (byte)'0');
                any = true;
            }

            value = parsed * sign;
            return any;
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            start = TrimStart(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            if (start >= end)
                return false;

            int sign = 1;
            if (bytes[start] == (byte)'-')
            {
                sign = -1;
                start++;
            }
            else if (bytes[start] == (byte)'+')
            {
                start++;
            }

            double parsed = 0.0;
            bool any = false;
            while (start < end)
            {
                byte c = bytes[start];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                parsed = (parsed * 10.0) + (c - (byte)'0');
                start++;
                any = true;
            }

            if (start < end && bytes[start] == (byte)'.')
            {
                start++;
                double scale = 0.1;
                while (start < end)
                {
                    byte c = bytes[start];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    parsed += (c - (byte)'0') * scale;
                    scale *= 0.1;
                    start++;
                    any = true;
                }
            }

            int exponent = 0;
            if (start < end && (bytes[start] == (byte)'e' || bytes[start] == (byte)'E'))
            {
                start++;
                int exponentSign = 1;
                if (start < end && bytes[start] == (byte)'-')
                {
                    exponentSign = -1;
                    start++;
                }
                else if (start < end && bytes[start] == (byte)'+')
                {
                    start++;
                }

                int exp = 0;
                bool hasExp = false;
                while (start < end)
                {
                    byte c = bytes[start];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    exp = (exp * 10) + (c - (byte)'0');
                    start++;
                    hasExp = true;
                }

                if (hasExp)
                    exponent = exp * exponentSign;
            }

            if (!any)
                return false;

            if (exponent != 0)
                parsed *= math.pow(10f, exponent);

            value = (float)(parsed * sign);
            return math.isfinite(value);
        }
    }
}

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct WaterlineBreachSignal : ISignal
    {
        [FieldOffset(0)] public double3 CameraAUP;
        [FieldOffset(24)] public float3 RuntimePosition;
        [FieldOffset(36)] public float SurfaceY;
        [FieldOffset(40)] public float CameraY;
        [FieldOffset(44)] public float Intensity01;
        [FieldOffset(48)] public uint SourceId;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public byte IsAboveSurface;
        [FieldOffset(57)] public byte Flags;
        [FieldOffset(58)] private ushort _pad0;
        [FieldOffset(60)] private uint _pad1;
    }
}
