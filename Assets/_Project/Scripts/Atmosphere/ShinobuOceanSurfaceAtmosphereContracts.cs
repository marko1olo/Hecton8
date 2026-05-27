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
        public const int WaveCapacity = 2;
        public const int WavesPerParameters = 3;
        public const int MaxWaveOctaves = WaveCapacity * WavesPerParameters;
        public const int MinQualityWaveCount = 1;
        public const int TelemetryFrameCount = 300;
        public const int WaveReadbackSampleCapacity = 64;
        public const int WaveReadbackRingSize = 3;
        public const int BeaufortProfileCapacity = 16;
        public const long TelemetryDumpBudgetNs = 500000L;
        public const uint SourceHash = 0x53485236u; // SHR6
        public const uint WaterlineBreachLaneHash = 0x57425236u; // WBR6
        public const uint QualityStepTuningHash = 0x51535450u; // QSTP
        public const float AuthoritativeQualityWeight = 1f;
        public const float DefaultSeaLevel = 0f;
        public const float MinimumWavelength = 0.25f;
        public const float TwoPi = 6.2831853071795864769f;
        public const ulong SeedShipActivatedNarrativeMask = 1UL << 61;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WaveParametersDTO
    {
        [FieldOffset(0)] public float4 Wave1;
        [FieldOffset(16)] public float4 Wave2;
        [FieldOffset(32)] public float4 Wave3;
        [FieldOffset(48)] public float4 GlobalWindAndStorm;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanWaveAupPhaseDTO
    {
        [FieldOffset(0)] public float4 PhaseBase0;
        [FieldOffset(16)] public float4 PhaseBase1;
        [FieldOffset(32)] public float4 CameraAupLocalXZ;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public float ActiveWaveCount;
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
        [FieldOffset(56)] public int ReadbackLatencyFrames;
        [FieldOffset(60)] public int ReadbackSampleCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BeaufortProfileDTO
    {
        [FieldOffset(0)] public uint StateHash;
        [FieldOffset(4)] public float BaseSteepness;
        [FieldOffset(8)] public float BaseWavelength;
        [FieldOffset(12)] public float WindSpeed;
        [FieldOffset(16)] public float StormIntensity;
        [FieldOffset(20)] public float FoamThreshold;
        [FieldOffset(24)] public float FrequencyScale;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float4 Reserved0;
        [FieldOffset(48)] public float4 Reserved1;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public static class HectonOceanSurfaceMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQualityWeight(float qualityWeight)
        {
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDesiredWaveCount(float qualityWeight, int maxWaveCount)
        {
            int maxCount = math.clamp(maxWaveCount, 1, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            float q = SanitizeQualityWeight(qualityWeight);
            float qualityCurve = q * q * (3f - (2f * q));
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static void EvaluateWaves(
            double3 AUP,
            float time,
            NativeArray<WaveParametersDTO> waves,
            out float height,
            out float3 normal)
        {
            EvaluateWaves(AUP, time, waves, 1f, out height, out normal);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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

            int waveLimit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            float dHeightDx = 0f;
            float dHeightDz = 0f;
            float minJacobian = 1f;

            for (int i = 0; i < waveLimit; i++)
            {
                float contribution = ResolveWaveContribution(i, globalQualityWeight, waveLimit);
                if (contribution <= 0.0001f)
                    continue;

                WaveParametersDTO wave = waves[i / OceanSurfaceAtmosphereConstants.WavesPerParameters];
                float4 lane = GetWaveLane(wave, i % OceanSurfaceAtmosphereConstants.WavesPerParameters);
                float amplitude = WaveLaneAmplitude(lane);
                float wavelength = WaveLaneWavelength(lane);
                if (amplitude <= 0.000001f)
                    continue;

                float2 direction = WaveLaneDirection(lane);
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / wavelength;
                double projected = (AUP.x * direction.x) + (AUP.z * direction.y);
                double wrappedMeters = WrapMeters(projected, wavelength);
                float phaseOffset = (i + 1) * 0.754877666f;
                float phase = WrapPhaseRadians((float)(wrappedMeters * waveNumber) + phaseOffset + (WaveLaneSpeed(lane) * time));

                Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(phase, out float sine, out float cosine);

                float weightedAmplitude = amplitude * contribution;
                height += weightedAmplitude * sine;
                activeWaveWeight += contribution;

                float slope = weightedAmplitude * waveNumber * cosine;
                dHeightDx += slope * direction.x;
                dHeightDz += slope * direction.y;

                float steepness = WaveLaneSteepness(lane);
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
            float q = SanitizeQualityWeight(globalQualityWeight);
            float qualityFoam = math.saturate((q - 0.28f) * (1f / 0.72f));
            qualityFoam = qualityFoam * qualityFoam * (3f - (2f * qualityFoam));
            float pinched = math.saturate((foamThreshold - jacobianDeterminant) * 4f);
            return pinched * qualityFoam;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanSurfaceLodDTO ResolveRadialGridLod(double3 cameraAup, float globalQualityWeight)
        {
            return ResolveRadialGridLod(cameraAup, globalQualityWeight, 4096f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanSurfaceLodDTO ResolveRadialGridLod(double3 cameraAup, float globalQualityWeight, float maxWavelength)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            float safeWavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, FinitePositive(maxWavelength, 4096f));
            OceanSurfaceLodDTO dto = default;
            dto.CameraAupLocalXZ = new float4((float)WrapMeters(cameraAup.x, safeWavelength), (float)WrapMeters(cameraAup.z, safeWavelength), safeWavelength, 0f);
            dto.GridParams = new float4(math.lerp(12f, 48f, q), math.lerp(36f, 144f, q), math.lerp(18f, 7f, q), math.lerp(64f, 224f, q));
            dto.RingParams = new float4(math.lerp(4f, 9f, q), math.lerp(1.85f, 1.38f, q), math.lerp(512f, 4096f, q), 0f);
            dto.GlobalQualityWeight = q;
            dto.HorizonMeters = math.lerp(900f, 5200f, q);
            dto.Flags = 1u;
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
                hash = HashFloat4(hash, wave.Wave1);
                hash = HashFloat4(hash, wave.Wave2);
                hash = HashFloat4(hash, wave.Wave3);
                hash = HashFloat4(hash, wave.GlobalWindAndStorm);
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
        public static float ResolveAupPhaseBaseRadians(double projectedMeters, float wavelength)
        {
            float safeWavelength = WaveLaneWavelength(new float4(0f, 0f, wavelength, 0f));
            double wrappedMeters = WrapMeters(projectedMeters, safeWavelength);
            return WrapPhaseRadians((float)(wrappedMeters * (OceanSurfaceAtmosphereConstants.TwoPi / safeWavelength)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanWaveAupPhaseDTO ResolveAupPhaseBases(
            double3 cameraAup,
            NativeArray<WaveParametersDTO> waves,
            uint frame,
            float globalQualityWeight,
            int activeWaveCount)
        {
            OceanWaveAupPhaseDTO dto = default;
            float q = SanitizeQualityWeight(globalQualityWeight);
            float maxWavelength = ResolveMaxWavelength(waves);
            double3 safeAup = math.all(math.isfinite(cameraAup)) ? cameraAup : double3.zero;
            dto.CameraAupLocalXZ = new float4(
                (float)WrapMeters(safeAup.x, maxWavelength),
                (float)WrapMeters(safeAup.z, maxWavelength),
                maxWavelength,
                0f);
            dto.Frame = frame;
            dto.Flags = math.all(math.isfinite(cameraAup)) ? 1u : 2u;
            dto.GlobalQualityWeight = q;
            dto.ActiveWaveCount = math.max(0, activeWaveCount);

            if (!waves.IsCreated)
                return dto;

            int laneLimit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                WaveParametersDTO wave = waves[waveIndex];
                for (int laneIndex = 0; laneIndex < OceanSurfaceAtmosphereConstants.WavesPerParameters; laneIndex++)
                {
                    int globalLaneIndex = (waveIndex * OceanSurfaceAtmosphereConstants.WavesPerParameters) + laneIndex;
                    if (globalLaneIndex >= laneLimit)
                        break;

                    float4 lane = GetWaveLane(wave, laneIndex);
                    float2 direction = WaveLaneDirection(lane);
                    float wavelength = WaveLaneWavelength(lane);
                    double projectedMeters = (safeAup.x * direction.x) + (safeAup.z * direction.y);
                    SetAupPhaseBase(ref dto, globalLaneIndex, ResolveAupPhaseBaseRadians(projectedMeters, wavelength));
                }
            }

            return dto;
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
            wave.Wave1 = SanitizeWaveLane(wave.Wave1);
            wave.Wave2 = SanitizeWaveLane(wave.Wave2);
            wave.Wave3 = SanitizeWaveLane(wave.Wave3);
            wave.GlobalWindAndStorm = math.select(float4.zero, wave.GlobalWindAndStorm, math.isfinite(wave.GlobalWindAndStorm));
            return wave;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 CreateWaveLane(float directionRadians, float steepness, float wavelength, float phaseSpeed)
        {
            return SanitizeWaveLane(new float4(directionRadians, steepness, wavelength, phaseSpeed));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 CreateWaveLaneFromDirection(float2 direction, float steepness, float wavelength, float phaseSpeed)
        {
            float2 safeDirection = Normalize2OrDefault(direction, new float2(1f, 0f));
            return CreateWaveLane(global::Hecton8.Core.MathLodApproximation.ApproxAtan2Fast(safeDirection.y, safeDirection.x), steepness, wavelength, phaseSpeed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetWaveLane(WaveParametersDTO wave, int laneIndex)
        {
            switch (laneIndex)
            {
                case 0:
                    return wave.Wave1;
                case 1:
                    return wave.Wave2;
                default:
                    return wave.Wave3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetWaveLane(ref WaveParametersDTO wave, int laneIndex, float4 lane)
        {
            switch (laneIndex)
            {
                case 0:
                    wave.Wave1 = SanitizeWaveLane(lane);
                    break;
                case 1:
                    wave.Wave2 = SanitizeWaveLane(lane);
                    break;
                default:
                    wave.Wave3 = SanitizeWaveLane(lane);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAupPhaseBase(OceanWaveAupPhaseDTO phase, int waveIndex)
        {
            switch (waveIndex)
            {
                case 0:
                    return math.isfinite(phase.PhaseBase0.x) ? phase.PhaseBase0.x : 0f;
                case 1:
                    return math.isfinite(phase.PhaseBase0.y) ? phase.PhaseBase0.y : 0f;
                case 2:
                    return math.isfinite(phase.PhaseBase0.z) ? phase.PhaseBase0.z : 0f;
                case 3:
                    return math.isfinite(phase.PhaseBase0.w) ? phase.PhaseBase0.w : 0f;
                case 4:
                    return math.isfinite(phase.PhaseBase1.x) ? phase.PhaseBase1.x : 0f;
                default:
                    return math.isfinite(phase.PhaseBase1.y) ? phase.PhaseBase1.y : 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAupPhaseBase(ref OceanWaveAupPhaseDTO phase, int waveIndex, float phaseBaseRadians)
        {
            float safePhase = WrapPhaseRadians(phaseBaseRadians);
            switch (waveIndex)
            {
                case 0:
                    phase.PhaseBase0.x = safePhase;
                    break;
                case 1:
                    phase.PhaseBase0.y = safePhase;
                    break;
                case 2:
                    phase.PhaseBase0.z = safePhase;
                    break;
                case 3:
                    phase.PhaseBase0.w = safePhase;
                    break;
                case 4:
                    phase.PhaseBase1.x = safePhase;
                    break;
                default:
                    phase.PhaseBase1.y = safePhase;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 WaveLaneDirection(float4 lane)
        {
            Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(math.isfinite(lane.x) ? lane.x : 0f, out float sine, out float cosine);
            return new float2(cosine, sine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WaveLaneSteepness(float4 lane)
        {
            return math.saturate(math.isfinite(lane.y) ? lane.y : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WaveLaneWavelength(float4 lane)
        {
            return math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, FinitePositive(lane.z, 24f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WaveLaneSpeed(float4 lane)
        {
            return math.isfinite(lane.w) ? lane.w : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WaveLaneAmplitude(float4 lane)
        {
            float steepness = WaveLaneSteepness(lane);
            if (steepness <= 0.000001f)
                return 0f;

            float wavelength = WaveLaneWavelength(lane);
            float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / wavelength;
            return math.min(wavelength * 0.125f, steepness / math.max(0.000001f, waveNumber));
        }

        public static float ResolveMaxWavelength(NativeArray<WaveParametersDTO> waves)
        {
            if (!waves.IsCreated || waves.Length <= 0)
                return 4096f;

            float maxWavelength = OceanSurfaceAtmosphereConstants.MinimumWavelength;
            int laneLimit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            for (int i = 0; i < laneLimit; i++)
            {
                float4 lane = GetWaveLane(waves[i / OceanSurfaceAtmosphereConstants.WavesPerParameters], i % OceanSurfaceAtmosphereConstants.WavesPerParameters);
                if (WaveLaneSteepness(lane) > 0.000001f)
                    maxWavelength = math.max(maxWavelength, WaveLaneWavelength(lane));
            }

            return maxWavelength;
        }

        public static float ResolveMaxAmplitude(NativeArray<WaveParametersDTO> waves)
        {
            if (!waves.IsCreated || waves.Length <= 0)
                return 0f;

            float maxAmplitude = 0f;
            int laneLimit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            for (int i = 0; i < laneLimit; i++)
            {
                float4 lane = GetWaveLane(waves[i / OceanSurfaceAtmosphereConstants.WavesPerParameters], i % OceanSurfaceAtmosphereConstants.WavesPerParameters);
                maxAmplitude = math.max(maxAmplitude, WaveLaneAmplitude(lane));
            }

            return maxAmplitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 SanitizeWaveLane(float4 lane)
        {
            lane.x = WrapPhaseRadians(math.isfinite(lane.x) ? lane.x : 0f);
            lane.y = math.saturate(math.isfinite(lane.y) ? lane.y : 0f);
            lane.z = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, FinitePositive(lane.z, 24f));
            lane.w = math.isfinite(lane.w) ? lane.w : 0f;
            return lane;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashFloat4(uint hash, float4 value)
        {
            hash = Hash(hash, math.asuint(value.x));
            hash = Hash(hash, math.asuint(value.y));
            hash = Hash(hash, math.asuint(value.z));
            return Hash(hash, math.asuint(value.w));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockStormJob : IJob
    {
        [NoAlias] public NativeArray<WaveParametersDTO> Waves;
        [NoAlias] public NativeArray<WeatherStateDTO> Weather;
        [NoAlias] public NativeArray<AtmosphereDTO> Atmosphere;
        [NoAlias] public NativeArray<float4> SurfaceSwell;
        public float SeaLevel;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public uint SimulationFrame;

        public void Execute()
        {
            if (!Weather.IsCreated || Weather.Length <= 0)
                return;

            float t = math.saturate((SimulationFrame & 1023u) * (1f / 1023f));
            float pulse = 0.5f + (0.5f * Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(TimeSeconds * 0.071f));
            WeatherStateDTO state = default;
            state.WindDirectionSpeedStorm = new float4(0.78f, 0.62f, math.lerp(9f, 28f, t), math.saturate(math.lerp(0.35f, 1f, pulse)));
            state.SurfaceScalars = new float4(SeaLevel, 1f, math.lerp(0.78f, 0.46f, state.WindDirectionSpeedStorm.w), math.saturate(state.WindDirectionSpeedStorm.w * 0.65f));
            state.SkyTintAndSurge = new float4(0.33f, 0.21f, 0.48f, math.lerp(0.08f, 1.35f, state.WindDirectionSpeedStorm.w));
            state.StateMask = 1u;
            state.GlobalQualityWeight = HectonOceanSurfaceMath.SanitizeQualityWeight(GlobalQualityWeight);
            Weather[0] = state;

            CalculateWaveParametersJob.FillWaveParameters(Waves, ref state, SurfaceSwell, default, TimeSeconds, GlobalQualityWeight);
            Weather[0] = state;

            if (Atmosphere.IsCreated && Atmosphere.Length > 0)
            {
                AtmosphereDTO atmo = default;
                atmo.RayleighBeta = new float4(0.0048f, 0.0118f, 0.0285f, 0f);
                atmo.MieBeta = new float4(0.021f, 0.018f, 0.014f, 0.72f);
                atmo.ScatteringParams = new float4(2.4f, 1.15f + state.WindDirectionSpeedStorm.w, 0.82f, 0.34f);
                atmo.PlanetParams = new float4(0.62f, 0.17f, 0.88f + (state.WindDirectionSpeedStorm.w * 0.35f), 0f);
                Atmosphere[0] = atmo;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CalculateWaveParametersJob : IJob
    {
        [NoAlias] public NativeArray<WaveParametersDTO> Waves;
        [NoAlias] public NativeArray<WeatherStateDTO> Weather;
        [NoAlias] public NativeArray<float4> SurfaceSwell;
        [ReadOnly, NoAlias] public NativeArray<BeaufortProfileDTO> TuningProfiles;
        public float TimeSeconds;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Weather.IsCreated || Weather.Length <= 0)
                return;

            WeatherStateDTO state = Weather[0];
            FillWaveParameters(Waves, ref state, SurfaceSwell, TuningProfiles, TimeSeconds, GlobalQualityWeight);
            Weather[0] = state;
        }

        public static void FillWaveParameters(
            NativeArray<WaveParametersDTO> waves,
            ref WeatherStateDTO state,
            NativeArray<float4> surfaceSwell,
            NativeArray<BeaufortProfileDTO> tuningProfiles,
            float timeSeconds,
            float globalQualityWeight)
        {
            float2 windDirection = HectonOceanSurfaceMath.Normalize2OrDefault(state.WindDirectionSpeedStorm.xy, new float2(1f, 0f));
            float windSpeed = math.max(0.01f, math.isfinite(state.WindDirectionSpeedStorm.z) ? state.WindDirectionSpeedStorm.z : 11f);
            float storm = math.saturate(math.isfinite(state.WindDirectionSpeedStorm.w) ? state.WindDirectionSpeedStorm.w : 0.42f);
            float choppiness = 0.52f;
            if (tuningProfiles.IsCreated &&
                tuningProfiles.Length > 0 &&
                tuningProfiles[0].StateHash == OceanSurfaceAtmosphereConstants.QualityStepTuningHash)
            {
                choppiness = math.saturate(tuningProfiles[0].StormIntensity);
            }

            float quality = HectonOceanSurfaceMath.SanitizeQualityWeight(globalQualityWeight);
            state.WindDirectionSpeedStorm = new float4(windDirection.x, windDirection.y, windSpeed, storm);
            state.GlobalQualityWeight = quality;

            if (!waves.IsCreated || waves.Length <= 0)
            {
                state.MaxWaveAmplitude = 0f;
                return;
            }

            float baseWavelength = math.lerp(18f, 48f, math.saturate(windSpeed * (1f / 32f)));
            float stormScale = math.lerp(0.65f, 1.85f, storm);
            float directionAngle = global::Hecton8.Core.MathLodApproximation.ApproxAtan2Fast(windDirection.y, windDirection.x);
            float maxAmplitude = 0f;
            int laneLimit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            float wavelengthScale = 1f;
            for (int i = 0; i < laneLimit; i++)
            {
                float octave01 = laneLimit <= 1 ? 0f : i * math.rcp(laneLimit - 1f);
                float wavelength = baseWavelength * wavelengthScale * stormScale;
                float spread = ((i & 1) == 0 ? -1f : 1f) * math.lerp(0.08f, 0.72f, octave01);
                float tunedSteepness = math.lerp(0.08f, 0.92f, choppiness);
                float steepness = math.saturate(tunedSteepness * math.lerp(0.72f, 1.18f, storm) * math.lerp(1.05f, 0.42f, octave01));
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, wavelength);
                float phaseSpeed = math.sqrt(9.81f * waveNumber) * math.lerp(0.82f, 1.22f, octave01);
                float4 lane = HectonOceanSurfaceMath.CreateWaveLane(directionAngle + spread, steepness, wavelength, phaseSpeed);
                int waveIndex = i / OceanSurfaceAtmosphereConstants.WavesPerParameters;
                int laneIndex = i - (waveIndex * OceanSurfaceAtmosphereConstants.WavesPerParameters);
                WaveParametersDTO wave = waves[waveIndex];
                HectonOceanSurfaceMath.SetWaveLane(ref wave, laneIndex, lane);
                wave.GlobalWindAndStorm = new float4(windDirection.x, windDirection.y, windSpeed, storm);
                waves[waveIndex] = HectonOceanSurfaceMath.SanitizeWave(wave);
                maxAmplitude = math.max(maxAmplitude, HectonOceanSurfaceMath.WaveLaneAmplitude(lane));
                wavelengthScale *= 1.58f;
            }

            state.MaxWaveAmplitude = maxAmplitude;
            if (surfaceSwell.IsCreated && surfaceSwell.Length > 0)
            {
                float swellSpeed = windSpeed * math.lerp(0.06f, 0.28f, storm) * math.lerp(0.5f, 1f, quality);
                surfaceSwell[0] = new float4(windDirection.x * swellSpeed, 0f, windDirection.y * swellSpeed, storm);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyBuoyancyJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float4> CompletedResults;
        [NoAlias] public NativeArray<float> Heights;
        public float SeaLevel;

        public void Execute(int index)
        {
            float height = 0f;
            if (CompletedResults.IsCreated && index < CompletedResults.Length)
                height = math.isfinite(CompletedResults[index].x) ? CompletedResults[index].x : 0f;

            if (Heights.IsCreated && index < Heights.Length)
                Heights[index] = SeaLevel + height;
        }
    }

#if UNITY_EDITOR
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

        public static bool TryApplyBeaufort(
            NativeArray<byte> csvBytes,
            int length,
            NativeArray<BeaufortProfileDTO> profiles)
        {
            if (!csvBytes.IsCreated || length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
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
                    int stateStart = TrimStart(csvBytes, rowStart, firstComma);
                    int stateEnd = TrimEnd(csvBytes, stateStart, firstComma);
                    uint stateHash = HashKey(csvBytes, stateStart, stateEnd);
                    if (TryParseBeaufortRow(csvBytes, firstComma + 1, rowEnd, stateHash, out BeaufortProfileDTO profile))
                        changed |= UpsertBeaufortProfile(profiles, profile);
                }

                rowStart = rowEnd + 1;
                while (rowStart < safeLength && (csvBytes[rowStart] == LineFeed || csvBytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        public static bool TryApplyBeaufort(ReadOnlySpan<byte> csvBytes, NativeArray<BeaufortProfileDTO> profiles)
        {
            if (csvBytes.Length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
                return false;

            bool changed = false;
            int rowStart = 0;
            while (rowStart < csvBytes.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < csvBytes.Length && csvBytes[rowEnd] != LineFeed && csvBytes[rowEnd] != CarriageReturn)
                    rowEnd++;

                int firstComma = FindByte(csvBytes, rowStart, rowEnd, Comma);
                if (firstComma > rowStart)
                {
                    int stateStart = TrimStart(csvBytes, rowStart, firstComma);
                    int stateEnd = TrimEnd(csvBytes, stateStart, firstComma);
                    uint stateHash = HashKey(csvBytes, stateStart, stateEnd);
                    if (TryParseBeaufortRow(csvBytes, firstComma + 1, rowEnd, stateHash, out BeaufortProfileDTO profile))
                        changed |= UpsertBeaufortProfile(profiles, profile);
                }

                rowStart = rowEnd + 1;
                while (rowStart < csvBytes.Length && (csvBytes[rowStart] == LineFeed || csvBytes[rowStart] == CarriageReturn))
                    rowStart++;
            }

            return changed;
        }

        private static bool TryParseBeaufortRow(NativeArray<byte> bytes, int start, int end, uint stateHash, out BeaufortProfileDTO profile)
        {
            profile = default;
            float windSpeed = 0f;
            float steepness = 0f;
            float wavelength = 0f;
            float storm = 0f;
            float foam = 0f;
            float frequency = 1f;
            int cursor = start;
            if (!TryParseNextFloat(bytes, ref cursor, end, out windSpeed) ||
                !TryParseNextFloat(bytes, ref cursor, end, out steepness) ||
                !TryParseNextFloat(bytes, ref cursor, end, out wavelength) ||
                !TryParseNextFloat(bytes, ref cursor, end, out storm) ||
                !TryParseNextFloat(bytes, ref cursor, end, out foam))
            {
                return false;
            }

            TryParseNextFloat(bytes, ref cursor, end, out frequency);
            profile.StateHash = stateHash;
            profile.WindSpeed = math.max(0f, windSpeed);
            profile.BaseSteepness = math.saturate(steepness);
            profile.BaseWavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, wavelength);
            profile.StormIntensity = math.saturate(storm);
            profile.FoamThreshold = math.saturate(foam);
            profile.FrequencyScale = math.max(0.01f, frequency);
            profile.Flags = 1u;
            return true;
        }

        private static bool TryParseNextFloat(NativeArray<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            if (cursor >= end)
                return false;

            int comma = FindByte(bytes, cursor, end, Comma);
            int valueEnd = comma >= 0 ? comma : end;
            bool parsed = TryParseFloat(bytes, cursor, valueEnd, out value);
            cursor = comma >= 0 ? comma + 1 : end;
            return parsed;
        }

        private static bool TryParseBeaufortRow(ReadOnlySpan<byte> bytes, int start, int end, uint stateHash, out BeaufortProfileDTO profile)
        {
            profile = default;
            float windSpeed = 0f;
            float steepness = 0f;
            float wavelength = 0f;
            float storm = 0f;
            float foam = 0f;
            float frequency = 1f;
            int cursor = start;
            if (!TryParseNextFloat(bytes, ref cursor, end, out windSpeed) ||
                !TryParseNextFloat(bytes, ref cursor, end, out steepness) ||
                !TryParseNextFloat(bytes, ref cursor, end, out wavelength) ||
                !TryParseNextFloat(bytes, ref cursor, end, out storm) ||
                !TryParseNextFloat(bytes, ref cursor, end, out foam))
            {
                return false;
            }

            TryParseNextFloat(bytes, ref cursor, end, out frequency);
            profile.StateHash = stateHash;
            profile.WindSpeed = math.max(0f, windSpeed);
            profile.BaseSteepness = math.saturate(steepness);
            profile.BaseWavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, wavelength);
            profile.StormIntensity = math.saturate(storm);
            profile.FoamThreshold = math.saturate(foam);
            profile.FrequencyScale = math.max(0.01f, frequency);
            profile.Flags = 1u;
            return true;
        }

        private static bool TryParseNextFloat(ReadOnlySpan<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            if (cursor >= end)
                return false;

            int comma = FindByte(bytes, cursor, end, Comma);
            int valueEnd = comma >= 0 ? comma : end;
            bool parsed = TryParseFloat(bytes, cursor, valueEnd, out value);
            cursor = comma >= 0 ? comma + 1 : end;
            return parsed;
        }

        private static bool UpsertBeaufortProfile(NativeArray<BeaufortProfileDTO> profiles, BeaufortProfileDTO profile)
        {
            if (profile.StateHash == 0u || !profiles.IsCreated || profiles.Length <= 0)
                return false;

            uint hash = profile.StateHash;
            int mask = profiles.Length - 1;
            int index = (profiles.Length & mask) == 0
                ? (int)(hash & (uint)mask)
                : (int)(hash % (uint)profiles.Length);
            for (int probe = 0; probe < profiles.Length; probe++)
            {
                int slot = (index + probe) % profiles.Length;
                BeaufortProfileDTO current = profiles[slot];
                if (current.StateHash == 0u || current.StateHash == profile.StateHash)
                {
                    profiles[slot] = profile;
                    return true;
                }
            }

            return false;
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

            int laneCapacity = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            int start = index >= 0 ? math.clamp(index, 0, laneCapacity - 1) : 0;
            int end = index >= 0 ? start + 1 : laneCapacity;
            for (int i = start; i < end; i++)
            {
                int waveIndex = i / OceanSurfaceAtmosphereConstants.WavesPerParameters;
                int laneIndex = i - (waveIndex * OceanSurfaceAtmosphereConstants.WavesPerParameters);
                WaveParametersDTO wave = waves[waveIndex];
                float4 lane = HectonOceanSurfaceMath.GetWaveLane(wave, laneIndex);
                switch (keyHash)
                {
                    case KeyWaveAmplitude:
                    {
                        float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / HectonOceanSurfaceMath.WaveLaneWavelength(lane);
                        lane.y = math.saturate(math.max(0f, value) * waveNumber);
                        break;
                    }
                    case KeyWaveWavelength:
                        lane.z = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, value);
                        break;
                    case KeyWaveSteepness:
                        lane.y = math.saturate(value);
                        break;
                    case KeyWavePhaseSpeed:
                        lane.w = value;
                        break;
                    case KeyWaveDirectionX:
                    {
                        float2 direction = HectonOceanSurfaceMath.WaveLaneDirection(lane);
                        direction.x = value;
                        lane = HectonOceanSurfaceMath.CreateWaveLaneFromDirection(direction, lane.y, lane.z, lane.w);
                        break;
                    }
                    case KeyWaveDirectionZ:
                    {
                        float2 direction = HectonOceanSurfaceMath.WaveLaneDirection(lane);
                        direction.y = value;
                        lane = HectonOceanSurfaceMath.CreateWaveLaneFromDirection(direction, lane.y, lane.z, lane.w);
                        break;
                    }
                }

                HectonOceanSurfaceMath.SetWaveLane(ref wave, laneIndex, lane);
                waves[waveIndex] = HectonOceanSurfaceMath.SanitizeWave(wave);
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

        private static int FindByte(ReadOnlySpan<byte> bytes, int start, int end, byte value)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == value)
                    return i;
            }

            return -1;
        }

        private static int TrimStart(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (start < end && (bytes[start] == Space || bytes[start] == Tab))
                start++;
            return start;
        }

        private static int TrimEnd(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (end > start && (bytes[end - 1] == Space || bytes[end - 1] == Tab))
                end--;
            return end;
        }

        private static uint HashKey(ReadOnlySpan<byte> bytes, int start, int end)
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
                parsed *= Pow10Int(exponent);

            value = (float)(parsed * sign);
            return math.isfinite(value);
        }

        private static double Pow10Int(int exponent)
        {
            int count = math.min(38, math.abs(exponent));
            double scale = 1.0d;
            for (int i = 0; i < count; i++)
                scale *= 10.0d;
            return exponent < 0 ? 1.0d / scale : scale;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
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
                parsed *= Pow10Int(exponent);

            value = (float)(parsed * sign);
            return math.isfinite(value);
        }
    }
#endif
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
