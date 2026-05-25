using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockHurricaneJob : IJob
    {
        [NoAlias] public NativeArray<MockHurricaneStateDTO> MockState;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public uint Seed;

        public void Execute()
        {
            if (!MockState.IsCreated || MockState.Length <= 0)
                return;

            float q = ShinobuStormPropagationMath.Sanitize01(GlobalQualityWeight);
            float time = math.isfinite(TimeSeconds) ? math.max(0f, TimeSeconds) : 0f;
            float seedPhase = (Seed & 1023u) * 0.006135923f;
            float2 direction = new float2(
                MathLodApproximation.ApproxCosBhaskara(time * 0.037f + seedPhase),
                MathLodApproximation.ApproxSinBhaskara(time * 0.029f + seedPhase * 1.37f));
            direction = math.normalizesafe(direction, new float2(1f, 0f));
            float pulse = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(time * math.lerp(0.031f, 0.073f, q) + seedPhase);
            float storm = math.saturate(math.lerp(0.64f, 0.96f, pulse) * math.lerp(0.82f, 1f, q));
            float windSpeed = math.lerp(28f, 58f, storm);

            ref MockHurricaneStateDTO mock = ref ShinobuStormPropagationNative.ElementAt(MockState, 0);
            mock.DirectionXZ = direction;
            mock.WindSpeedMetersPerSecond = windSpeed;
            mock.StormIntensity01 = storm;
            mock.RainIntensity01 = math.lerp(0.55f, 1f, storm);
            mock.SurfaceSurge01 = math.lerp(0.35f, 1f, storm);
            mock.Flags = 1u;
            mock.Seed = Seed;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateStormAttenuationJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<WeatherStateDTO> WeatherState;
        [ReadOnly, NoAlias] public NativeArray<StormPropagationTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<StormDepthImpactProfileDTO> Profiles;
        [ReadOnly, NoAlias] public NativeArray<MockHurricaneStateDTO> MockWeather;
        [NoAlias] public NativeArray<StormPropagationWriteSnapshotDTO> WriteSnapshot;
        [NoAlias] public NativeArray<StormPropagationTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public double3 SampleAup;
        public double3 SeaLevelAup;
        public float PreviousSurfaceIntensity01;
        public float DeltaTime;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public uint Frame;
        public uint ForceFlags;
        public int UseMockWeather;

        public void Execute()
        {
            if (!WriteSnapshot.IsCreated || WriteSnapshot.Length <= 0)
                return;

            StormPropagationTuningDTO tuning = ResolveTuning();
            WeatherStateDTO weather = ResolveWeather();
            float quality = math.isfinite(GlobalQualityWeight)
                ? ShinobuStormPropagationMath.Sanitize01(GlobalQualityWeight)
                : ShinobuStormPropagationMath.Sanitize01(tuning.GlobalQualityWeight);
            if (!math.isfinite(quality))
                quality = ShinobuStormPropagationMath.Sanitize01(tuning.GlobalQualityWeight);

            float depth = ShinobuStormPropagationMath.ResolveDepthMeters(SampleAup, SeaLevelAup);
            float storm = ShinobuStormPropagationMath.Sanitize01(weather.WindDirectionSpeedStorm.w);
            uint stateMask = weather.StateMask;
            if (UseMockWeather != 0 && MockWeather.IsCreated && MockWeather.Length > 0)
            {
                MockHurricaneStateDTO mock = ShinobuStormPropagationNative.ReadElement(MockWeather, 0);
                if (mock.Flags != 0u && math.isfinite(mock.StormIntensity01))
                {
                    weather.WindDirectionSpeedStorm = new float4(
                        mock.DirectionXZ.x,
                        mock.DirectionXZ.y,
                        mock.WindSpeedMetersPerSecond,
                        mock.StormIntensity01);
                    storm = ShinobuStormPropagationMath.Sanitize01(mock.StormIntensity01);
                    stateMask |= ShinobuStormPropagationConstants.WeatherMaskStorm;
                    ForceFlags |= ShinobuStormPropagationConstants.TelemetryFlagMockWeather;
                }
            }

            tuning = ApplyProfileForDepth(tuning, depth, stateMask, storm);
            float decay = math.max(0.000001f, tuning.DecayConstant);
            float energy = ShinobuStormPropagationMath.Attenuate(storm, depth, decay);
            float time = math.isfinite(TimeSeconds) ? TimeSeconds : 0f;
            float2 direction = math.normalizesafe(weather.WindDirectionSpeedStorm.xy, new float2(1f, 0f));
            float windSpeed = math.max(0f, math.isfinite(weather.WindDirectionSpeedStorm.z) ? weather.WindDirectionSpeedStorm.z : 0f);
            float2 samplePhase = new float2(
                (float)math.fmod(SampleAup.x, 4096d),
                (float)math.fmod(SampleAup.z, 4096d));
            float noise = ShinobuStormPropagationMath.ResolveContinuousNoise(samplePhase, time, quality);
            float surgeGain = math.max(0f, tuning.SurgeScale) * math.lerp(0.72f, 1.28f, ShinobuStormPropagationMath.Smooth01(quality));
            float surgeMagnitude = windSpeed * energy * surgeGain * math.lerp(1f - math.max(0f, tuning.NoiseScale) * 0.18f, 1f + math.max(0f, tuning.NoiseScale) * 0.18f, noise);
            float3 surge = new float3(direction.x * surgeMagnitude, 0f, direction.y * surgeMagnitude);

            float maxTurbidity = math.max(1f, tuning.FogBaseDensityExtinction.z);
            float turbidity = math.clamp(1f + (energy * math.max(0f, tuning.TurbidityGain)), 1f, maxTurbidity);
            float acoustic = math.saturate(energy * math.max(0f, tuning.AcousticMufflingGain) * math.lerp(0.65f, 1.15f, ShinobuStormPropagationMath.Smooth01(depth * math.rcp(math.max(1f, tuning.MaxDepthMeters)))));
            float intensityDelta = math.abs(storm - ShinobuStormPropagationMath.Sanitize01(PreviousSurfaceIntensity01));
            float dt = math.max(0.0166667f, math.isfinite(DeltaTime) ? DeltaTime : 0.0166667f);
            float biolum = math.saturate(intensityDelta * math.rcp(dt) * 0.08f * math.max(0f, tuning.BiolumDeltaGain) + energy * 0.18f);
            int noiseOctaves = ShinobuStormPropagationMath.ResolveNoiseOctaveCount(quality);

            uint flags = ForceFlags;
            if (!math.all(math.isfinite(surge)) ||
                !math.isfinite(turbidity) ||
                !math.isfinite(acoustic) ||
                !math.isfinite(biolum) ||
                !math.isfinite(depth) ||
                !math.isfinite(energy))
            {
                flags |= ShinobuStormPropagationConstants.TelemetryFlagNonFinite;
                surge = float3.zero;
                turbidity = 1f;
                acoustic = 0f;
                biolum = 0f;
                energy = 0f;
            }

            StormPropagationDTO dto = new StormPropagationDTO
            {
                SurgeVector = surge,
                TurbidityScalar = turbidity,
                AcousticMuffling = acoustic,
                BioluminescenceStimulus = biolum
            };

            float4 flowScalar = new float4(surge, energy);
            float lowPass = math.lerp(22000f, math.max(80f, tuning.MinimumLowPassHertz), acoustic);
            float4 audioScalar = new float4(acoustic, lowPass, energy, depth);
            float pulseMultiplier = 1f + biolum * math.lerp(0.18f, 0.72f, ShinobuStormPropagationMath.Smooth01(quality));
            float4 biolumScalar = new float4(biolum, pulseMultiplier, energy, depth);
            float fogMultiplier = math.clamp(turbidity, 1f, maxTurbidity);
            float flowAdvection = math.clamp(tuning.FogBaseDensityExtinction.w + math.length(surge) * 0.08f, 0f, 8f);
            float4 fogScalar = new float4(fogMultiplier, fogMultiplier, flowAdvection, energy);

            StormPropagationWriteSnapshotDTO snapshot = new StormPropagationWriteSnapshotDTO
            {
                State = dto,
                FlowScalar = flowScalar,
                AudioScalar = audioScalar,
                BiolumScalar = biolumScalar,
                FogScalar = fogScalar
            };

            void* writePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(WriteSnapshot);
            void* snapshotPtr = UnsafeUtility.AddressOf(ref snapshot);
            UnsafeUtility.MemCpy(writePtr, snapshotPtr, ShinobuStormPropagationConstants.WriteSnapshotStrideBytes);

            WriteTelemetry(flags, storm, depth, energy, turbidity, acoustic, biolum, surge, quality, noiseOctaves);
        }

        private StormPropagationTuningDTO ResolveTuning()
        {
            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                StormPropagationTuningDTO tuning = ShinobuStormPropagationNative.ReadElement(Tuning, 0);
                return ShinobuStormPropagationNative.SanitizeTuning(tuning, GlobalQualityWeight);
            }

            return ShinobuStormPropagationNative.CreateDefaultTuning(GlobalQualityWeight);
        }

        private WeatherStateDTO ResolveWeather()
        {
            if (!WeatherState.IsCreated || WeatherState.Length <= 0)
                return default;

            return ShinobuStormPropagationNative.ReadElement(WeatherState, 0);
        }

        private StormPropagationTuningDTO ApplyProfileForDepth(StormPropagationTuningDTO tuning, float depth, uint stateMask, float stormIntensity01)
        {
            if (!Profiles.IsCreated || Profiles.Length <= 0)
                return tuning;

            float totalWeight = 0f;
            float decay = 0f;
            float turbidity = 0f;
            float surge = 0f;
            float acoustic = 0f;
            float biolum = 0f;
            float bestWeight = 0f;
            uint profileHash = tuning.ProfileHash;
            for (int i = 0; i < Profiles.Length; i++)
            {
                StormDepthImpactProfileDTO profile = ShinobuStormPropagationNative.ReadElement(Profiles, i);
                if (profile.ProfileHash == 0u)
                    continue;

                float minDepth = math.max(0f, profile.MinDepthMeters);
                float maxDepth = math.max(minDepth + 1f, profile.MaxDepthMeters);
                float fade = math.clamp((maxDepth - minDepth) * 0.12f, 1f, 128f);
                float enter = math.smoothstep(minDepth - fade, minDepth + fade, depth);
                float exit = 1f - math.smoothstep(maxDepth - fade, maxDepth + fade, depth);
                float profileWeight = ShinobuStormPropagationMath.ResolveWeatherProfileWeight(profile.ProfileHash, stateMask, stormIntensity01);
                float weight = enter * exit * profileWeight;
                if (weight <= 0f)
                    continue;

                totalWeight += weight;
                decay += math.max(0.000001f, profile.DecayConstant) * weight;
                turbidity += math.max(0f, profile.TurbidityGain) * weight;
                surge += math.max(0f, profile.SurgeScale) * weight;
                acoustic += math.max(0f, profile.AcousticGain) * weight;
                biolum += math.max(0f, profile.BiolumGain) * weight;
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    profileHash = profile.ProfileHash;
                }
            }

            if (totalWeight <= 0f)
                return tuning;

            float invWeight = math.rcp(math.max(0.0001f, totalWeight));
            tuning.DecayConstant = decay * invWeight;
            tuning.TurbidityGain = turbidity * invWeight;
            tuning.SurgeScale = surge * invWeight;
            tuning.AcousticMufflingGain = acoustic * invWeight;
            tuning.BiolumDeltaGain = biolum * invWeight;
            tuning.ProfileHash = profileHash;
            return tuning;
        }

        private void WriteTelemetry(
            uint flags,
            float storm,
            float depth,
            float energy,
            float turbidity,
            float acoustic,
            float biolum,
            float3 surge,
            float quality,
            int noiseOctaves)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int index = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                ref int cursorRef = ref ShinobuStormPropagationNative.ElementAt(TelemetryCursor, 0);
                int cursor = cursorRef;
                index = ShinobuStormPropagationMath.WrapRingIndex(cursor, Telemetry.Length);
                cursorRef = ShinobuStormPropagationMath.AdvanceRingCursor(cursor, Telemetry.Length);
            }
            else
            {
                index = (int)(Frame % (uint)Telemetry.Length);
            }

            ref StormPropagationTelemetryEntry entry = ref ShinobuStormPropagationNative.ElementAt(Telemetry, index);
            entry = new StormPropagationTelemetryEntry
            {
                Frame = Frame,
                Flags = flags,
                SurfaceIntensity01 = storm,
                DepthMeters = depth,
                AttenuatedEnergy01 = energy,
                TurbidityScalar = turbidity,
                AcousticMuffling01 = acoustic,
                BiolumStimulus01 = biolum,
                SurgeVector = surge,
                GlobalQualityWeight = quality,
                ScheduleToPublishMicroseconds = 0f,
                PreviousSurfaceIntensity01 = PreviousSurfaceIntensity01,
                StateHash = ShinobuStormPropagationMath.HashState(depth, energy, turbidity, acoustic, biolum),
                NoiseOctaveCount = noiseOctaves
            };
        }
    }
}
