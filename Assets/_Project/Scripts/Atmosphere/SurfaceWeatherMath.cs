using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Hecton8.Core;
using Hecton8.Core.Contracts;

namespace Hecton8.Atmosphere
{
    internal struct SurfaceWeatherMathState
    {
        public float cloudDensityThreshold;
        public float cloudSoftness;
        public float cloudSpeedMultiplier;
        public float2 windDirection;
        public float skyLuminanceMultiplier;
        public float starVisibilityMultiplier;
        public float stormEmissionMultiplier;
        public float4 cloudLitColor;
        public float4 cloudShadowColor;
        public float4 sunsetCloudColor;
        public float4 nightCloudColor;
        public float4 surfaceFogColor;
        public float surfaceFogDensity;
        public float4 surfaceAmbientColor;
        public float surfaceSunMultiplier;
        public float sunDiscMultiplier;
        public float sunScatterMultiplier;
        public float oceanWindSpeedKmh;
        public float oceanFoamStrength;
        public float oceanFoamCoverage;
        public float oceanFoamScale;
        public float precipitationIntensity;
        public float electricalActivity;
        public float lightningFlashIntensity;
        public float lightningFlashDuration;
        public float thunderDelayMin;
        public float thunderDelayMax;
        public float lightningStrikeDistanceMin;
        public float lightningStrikeDistanceMax;
        public float lightningWindBias;
        public float thunderPropagationDistanceScale;
        public float thunderVolumeNear;
        public float thunderVolumeFar;
        public float thunderPitchMin;
        public float thunderPitchMax;
        public float localRainAreaScale;
        public float localRainDensityMultiplier;
        public float surfaceImpactRadiusScale;
        public float surfaceImpactDensityMultiplier;
        public float lightningBoltWidthMultiplier;
        public float lightningLightRangeMultiplier;
        public float gustStrength;
        public float gustFrequency;
        public float squallStrength;
        public float squallFrequency;

        public static SurfaceWeatherMathState Lerp(in SurfaceWeatherMathState from, in SurfaceWeatherMathState to, float t)
        {
            return new SurfaceWeatherMathState
            {
                cloudDensityThreshold = math.lerp(from.cloudDensityThreshold, to.cloudDensityThreshold, t),
                cloudSoftness = math.lerp(from.cloudSoftness, to.cloudSoftness, t),
                cloudSpeedMultiplier = math.lerp(from.cloudSpeedMultiplier, to.cloudSpeedMultiplier, t),
                windDirection = math.lerp(from.windDirection, to.windDirection, t),
                skyLuminanceMultiplier = math.lerp(from.skyLuminanceMultiplier, to.skyLuminanceMultiplier, t),
                starVisibilityMultiplier = math.lerp(from.starVisibilityMultiplier, to.starVisibilityMultiplier, t),
                stormEmissionMultiplier = math.lerp(from.stormEmissionMultiplier, to.stormEmissionMultiplier, t),
                cloudLitColor = math.lerp(from.cloudLitColor, to.cloudLitColor, t),
                cloudShadowColor = math.lerp(from.cloudShadowColor, to.cloudShadowColor, t),
                sunsetCloudColor = math.lerp(from.sunsetCloudColor, to.sunsetCloudColor, t),
                nightCloudColor = math.lerp(from.nightCloudColor, to.nightCloudColor, t),
                surfaceFogColor = math.lerp(from.surfaceFogColor, to.surfaceFogColor, t),
                surfaceFogDensity = math.lerp(from.surfaceFogDensity, to.surfaceFogDensity, t),
                surfaceAmbientColor = math.lerp(from.surfaceAmbientColor, to.surfaceAmbientColor, t),
                surfaceSunMultiplier = math.lerp(from.surfaceSunMultiplier, to.surfaceSunMultiplier, t),
                sunDiscMultiplier = math.lerp(from.sunDiscMultiplier, to.sunDiscMultiplier, t),
                sunScatterMultiplier = math.lerp(from.sunScatterMultiplier, to.sunScatterMultiplier, t),
                oceanWindSpeedKmh = math.lerp(from.oceanWindSpeedKmh, to.oceanWindSpeedKmh, t),
                oceanFoamStrength = math.lerp(from.oceanFoamStrength, to.oceanFoamStrength, t),
                oceanFoamCoverage = math.lerp(from.oceanFoamCoverage, to.oceanFoamCoverage, t),
                oceanFoamScale = math.lerp(from.oceanFoamScale, to.oceanFoamScale, t),
                precipitationIntensity = math.lerp(from.precipitationIntensity, to.precipitationIntensity, t),
                electricalActivity = math.lerp(from.electricalActivity, to.electricalActivity, t),
                lightningFlashIntensity = math.lerp(from.lightningFlashIntensity, to.lightningFlashIntensity, t),
                lightningFlashDuration = math.lerp(from.lightningFlashDuration, to.lightningFlashDuration, t),
                thunderDelayMin = math.lerp(from.thunderDelayMin, to.thunderDelayMin, t),
                thunderDelayMax = math.lerp(from.thunderDelayMax, to.thunderDelayMax, t),
                lightningStrikeDistanceMin = math.lerp(from.lightningStrikeDistanceMin, to.lightningStrikeDistanceMin, t),
                lightningStrikeDistanceMax = math.lerp(from.lightningStrikeDistanceMax, to.lightningStrikeDistanceMax, t),
                lightningWindBias = math.lerp(from.lightningWindBias, to.lightningWindBias, t),
                thunderPropagationDistanceScale = math.lerp(from.thunderPropagationDistanceScale, to.thunderPropagationDistanceScale, t),
                thunderVolumeNear = math.lerp(from.thunderVolumeNear, to.thunderVolumeNear, t),
                thunderVolumeFar = math.lerp(from.thunderVolumeFar, to.thunderVolumeFar, t),
                thunderPitchMin = math.lerp(from.thunderPitchMin, to.thunderPitchMin, t),
                thunderPitchMax = math.lerp(from.thunderPitchMax, to.thunderPitchMax, t),
                localRainAreaScale = math.lerp(from.localRainAreaScale, to.localRainAreaScale, t),
                localRainDensityMultiplier = math.lerp(from.localRainDensityMultiplier, to.localRainDensityMultiplier, t),
                surfaceImpactRadiusScale = math.lerp(from.surfaceImpactRadiusScale, to.surfaceImpactRadiusScale, t),
                surfaceImpactDensityMultiplier = math.lerp(from.surfaceImpactDensityMultiplier, to.surfaceImpactDensityMultiplier, t),
                lightningBoltWidthMultiplier = math.lerp(from.lightningBoltWidthMultiplier, to.lightningBoltWidthMultiplier, t),
                lightningLightRangeMultiplier = math.lerp(from.lightningLightRangeMultiplier, to.lightningLightRangeMultiplier, t),
                gustStrength = math.lerp(from.gustStrength, to.gustStrength, t),
                gustFrequency = math.lerp(from.gustFrequency, to.gustFrequency, t),
                squallStrength = math.lerp(from.squallStrength, to.squallStrength, t),
                squallFrequency = math.lerp(from.squallFrequency, to.squallFrequency, t)
            };
        }
    }

    internal struct SurfaceWeatherBindingSnapshot
    {
        public float gustMultiplier;
        public float squallMultiplier;
        public float localRainExposure;
        public float skyLuminance;
        public float sunDisc;
        public float sunScatter;
        public float sunMultiplier;
        public float cloudSpeedMultiplier;
        public float vfxPrecipitation;
        public float acousticPrecipitation;
        public float localRainAreaScale;
        public float localRainDensityMultiplier;
        public float surfaceImpactRadiusScale;
        public float surfaceImpactDensityMultiplier;
        public float targetWindSpeed;
        public float targetFoamStrength;
        public float targetFoamCoverage;
        public float targetFoamScale;
    }

    internal struct SurfaceWeatherJobInput
    {
        public SurfaceWeatherMathState currentState;
        public SurfaceWeatherMathState targetState;
        public float deltaTime;
        public float weatherBlendDuration;
        public byte executionMode;
        public float currentLocalRainExposure;
        public float targetLocalRainExposure;
        public float shelterExposureBlendTime;
        public float lightningCooldown;
        public float lightningFlashRemaining;
        public float lightningFlashStrength;
        public float pendingThunderDelay;
        public float pendingThunderVolume;
        public float pendingThunderPitch;
        public float3 pendingThunderPosition;
        public float gustTimeOffset;
        public float unscaledTime;
        public float stormEquipmentPulseTimer;
        public float stormInterferenceElectricalThreshold;
        public float stormInterferencePulseIntervalMin;
        public float stormInterferencePulseIntervalMax;
        public float3 followPosition;
        public double3 absoluteUniverseOffset;
        public float surfaceY;
        public uint randomState;
        public float defaultFoamStrength;
        public float defaultFoamCoverage;
        public float defaultFoamScale;
    }

    internal struct SurfaceWeatherJobOutput
    {
        public SurfaceWeatherMathState currentState;
        public SurfaceWeatherBindingSnapshot bindings;
        public float currentLocalRainExposure;
        public float lightningCooldown;
        public float lightningFlashRemaining;
        public float lightningFlashStrength;
        public float pendingThunderDelay;
        public float pendingThunderVolume;
        public float pendingThunderPitch;
        public float3 pendingThunderPosition;
        public float stormEquipmentPulseTimer;
        public float3 lightningImpactPosition;
        public float lightningPhaseA;
        public float lightningPhaseB;
        public float lightningLightRange;
        public float lightningBoltWidth;
        public float stormPulseIntensity;
        public uint randomState;
        public byte shouldTriggerLightning;
        public byte shouldTriggerPassiveStormPulse;
        public byte shouldTriggerLightningStormPulse;
        public byte shouldPlayThunder;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct SurfaceWeatherMathJob : IJob
    {
        private const byte SurfaceExecutionModeSurfaceActive = 0;
        private const byte SurfaceExecutionModeSurfaceSuppressed = 2;
        private const float TwoPi = 6.283185307179586f;
        private const float LightningFlashSeconds = 0.1f;
        private const float SpeedOfSoundMetersPerSecond = HectonPhysicsContract.SoundSpeedAirMetersPerSecondConst;

        public SurfaceWeatherJobInput input;
        public NativeSlice<SurfaceWeatherJobOutput> output;

        public void Execute()
        {
            SurfaceWeatherJobOutput result = default;
            SurfaceWeatherMathState state = SurfaceWeatherMathState.Lerp(
                in input.currentState,
                in input.targetState,
                math.saturate(input.deltaTime / math.max(input.weatherBlendDuration, 0.0001f)));

            result.currentState = state;
            result.currentLocalRainExposure = math.lerp(
                input.currentLocalRainExposure,
                input.targetLocalRainExposure,
                math.saturate(input.deltaTime / math.max(input.shelterExposureBlendTime, 0.05f)));

            result.pendingThunderDelay = input.pendingThunderDelay;
            result.pendingThunderVolume = input.pendingThunderVolume;
            result.pendingThunderPitch = input.pendingThunderPitch;
            result.pendingThunderPosition = input.pendingThunderPosition;
            result.lightningCooldown = input.lightningCooldown;
            result.lightningFlashRemaining = input.lightningFlashRemaining;
            result.lightningFlashStrength = input.lightningFlashStrength;
            result.stormEquipmentPulseTimer = input.stormEquipmentPulseTimer;
            result.randomState = input.randomState;

            if (result.lightningFlashRemaining > 0f)
            {
                result.lightningFlashRemaining -= input.deltaTime;
                if (result.lightningFlashRemaining <= 0f)
                {
                    result.lightningFlashRemaining = 0f;
                    result.lightningFlashStrength = 0f;
                }
            }

            if (result.pendingThunderDelay >= 0f)
            {
                result.pendingThunderDelay -= input.deltaTime;
                if (result.pendingThunderDelay <= 0f)
                {
                    result.pendingThunderDelay = -1f;
                    result.shouldPlayThunder = 1;
                }
            }

            float electricalActivity = math.saturate(state.electricalActivity);
            float gustMultiplier = ResolveGustMultiplier(state, input.unscaledTime, input.gustTimeOffset);
            float squallMultiplier = ResolveSquallMultiplier(state, input.unscaledTime, input.gustTimeOffset);
            byte surfaceVfxActive = input.executionMode == SurfaceExecutionModeSurfaceActive ? (byte)1 : (byte)0;
            float localRainExposure = surfaceVfxActive != 0 ? math.saturate(result.currentLocalRainExposure) : 0f;
            float flashStrength = math.max(0f, result.lightningFlashStrength);
            float lightningDirectionalLightMultiplier = math.lerp(1f, 5f, math.saturate(flashStrength));

            if (electricalActivity > 0.2f)
            {
                result.lightningCooldown -= input.deltaTime;
                if (result.lightningCooldown <= 0f)
                {
                    TriggerLightning(
                        ref result,
                        state,
                        electricalActivity,
                        gustMultiplier,
                        input.followPosition,
                        input.absoluteUniverseOffset,
                        input.surfaceY);
                }
            }

            float stormInterference = ResolveStormInterference(
                state.precipitationIntensity,
                electricalActivity,
                input.stormInterferenceElectricalThreshold);

            if (input.executionMode == SurfaceExecutionModeSurfaceSuppressed || stormInterference <= 0f)
            {
                result.stormEquipmentPulseTimer = 0f;
            }
            else
            {
                result.stormEquipmentPulseTimer -= input.deltaTime;
                if (result.stormEquipmentPulseTimer <= 0f)
                {
                    result.shouldTriggerPassiveStormPulse = 1;
                    result.stormPulseIntensity = stormInterference;
                    result.stormEquipmentPulseTimer = math.lerp(
                        math.max(0.05f, input.stormInterferencePulseIntervalMax),
                        math.max(0.05f, input.stormInterferencePulseIntervalMin),
                        stormInterference);
                }
            }

            result.bindings = new SurfaceWeatherBindingSnapshot
            {
                gustMultiplier = gustMultiplier,
                squallMultiplier = squallMultiplier,
                localRainExposure = localRainExposure,
                skyLuminance = math.max(0f, state.skyLuminanceMultiplier + flashStrength * 0.22f),
                sunDisc = math.max(0f, state.sunDiscMultiplier + flashStrength),
                sunScatter = math.max(0f, state.sunScatterMultiplier + flashStrength * 0.35f),
                sunMultiplier = math.max(0f, state.surfaceSunMultiplier) * lightningDirectionalLightMultiplier,
                cloudSpeedMultiplier = state.cloudSpeedMultiplier * math.lerp(1f, gustMultiplier, 0.35f),
                vfxPrecipitation = surfaceVfxActive != 0
                    ? math.saturate(state.precipitationIntensity * math.lerp(1f, gustMultiplier, 0.4f) * squallMultiplier * localRainExposure)
                    : 0f,
                acousticPrecipitation = math.saturate(state.precipitationIntensity * squallMultiplier),
                localRainAreaScale = state.localRainAreaScale * math.lerp(1f, gustMultiplier, 0.18f) * math.lerp(1f, squallMultiplier, 0.08f),
                localRainDensityMultiplier = state.localRainDensityMultiplier * math.lerp(1f, gustMultiplier, 0.35f) * squallMultiplier * math.lerp(0.4f, 1f, localRainExposure),
                surfaceImpactRadiusScale = state.surfaceImpactRadiusScale * math.lerp(1f, gustMultiplier, 0.12f) * math.lerp(1f, squallMultiplier, 0.15f),
                surfaceImpactDensityMultiplier = state.surfaceImpactDensityMultiplier * math.lerp(1f, gustMultiplier, 0.42f) * squallMultiplier * localRainExposure,
                targetWindSpeed = math.max(0f, state.oceanWindSpeedKmh * math.lerp(1f, gustMultiplier, 0.42f)),
                targetFoamStrength = input.defaultFoamStrength * math.max(0f, state.oceanFoamStrength * math.lerp(1f, gustMultiplier, 0.24f)),
                targetFoamCoverage = input.defaultFoamCoverage * math.max(0f, state.oceanFoamCoverage * math.lerp(1f, gustMultiplier, 0.18f)),
                targetFoamScale = input.defaultFoamScale * math.max(0.1f, state.oceanFoamScale * math.lerp(1f, gustMultiplier, 0.08f))
            };

            output[0] = result;
        }

        private static void TriggerLightning(
            ref SurfaceWeatherJobOutput result,
            in SurfaceWeatherMathState state,
            float electricalActivity,
            float gustMultiplier,
            float3 followPosition,
            double3 absoluteUniverseOffset,
            float surfaceY)
        {
            float flashDuration = LightningFlashSeconds;
            float flashBase = math.max(0f, state.lightningFlashIntensity);
            float randomA = NextRandom01(ref result.randomState);
            float randomB = NextRandom01(ref result.randomState);
            float randomC = NextRandom01(ref result.randomState);
            float flashVariance = math.lerp(0.7f, 1f, randomA);

            float2 preferredDirection = state.windDirection;
            float preferredLengthSq = math.lengthsq(preferredDirection);
            if (preferredLengthSq < 0.0001f)
                preferredDirection = new float2(1f, 0f);
            else
                preferredDirection *= math.rsqrt(preferredLengthSq);

            float randomAngle = randomB * TwoPi;
            float2 randomDirection = new float2(CinematicMath.FastCos(randomAngle), CinematicMath.FastSin(randomAngle));
            float clampedWindBias = math.saturate(state.lightningWindBias);
            float angularOffset = ((randomC * 2f) - 1f) * math.lerp(1.4f, 0.35f, clampedWindBias);
            float2 windBiasedDirection = RotateDirection(preferredDirection, angularOffset);
            float2 resolvedDirection = math.lerp(randomDirection, windBiasedDirection, clampedWindBias);
            float resolvedLengthSq = math.lengthsq(resolvedDirection);
            if (resolvedLengthSq < 0.0001f)
                resolvedDirection = randomDirection;
            else
                resolvedDirection *= math.rsqrt(resolvedLengthSq);

            float minDistance = math.max(10f, state.lightningStrikeDistanceMin);
            float maxDistance = math.max(minDistance, state.lightningStrikeDistanceMax);
            float distance = math.lerp(minDistance, maxDistance, randomA);
            float3 strikePosition = followPosition + new float3(resolvedDirection.x, 0f, resolvedDirection.y) * distance;
            strikePosition.y = surfaceY;

            float thunderDistance = ResolveAupThunderDistanceMeters(
                followPosition,
                strikePosition,
                absoluteUniverseOffset);
            float distanceT = math.saturate((thunderDistance - minDistance) / math.max(maxDistance - minDistance, 0.0001f));
            float loudness = math.lerp(state.thunderVolumeNear, state.thunderVolumeFar, distanceT);
            float stormBoost = math.lerp(0.65f, 1f, electricalActivity);
            float thunderDelay = thunderDistance / SpeedOfSoundMetersPerSecond;

            result.lightningFlashRemaining = flashDuration;
            result.lightningFlashStrength = flashBase * flashVariance;
            result.lightningCooldown = math.max(
                0.5f,
                math.lerp(18f, 4.5f, electricalActivity) +
                ((NextRandom01(ref result.randomState) * 2f) - 1f) * math.lerp(8f, 1.5f, electricalActivity));
            result.pendingThunderPosition = strikePosition;
            result.pendingThunderDelay = thunderDelay;
            result.pendingThunderVolume = loudness * stormBoost;
            result.pendingThunderPitch = math.lerp(state.thunderPitchMin, state.thunderPitchMax, NextRandom01(ref result.randomState)) *
                math.lerp(0.94f, 1.02f, 1f - distanceT);
            result.lightningImpactPosition = strikePosition;
            result.lightningPhaseA = randomA;
            result.lightningPhaseB = randomB;
            result.lightningLightRange = state.lightningLightRangeMultiplier * math.lerp(1f, gustMultiplier, 0.08f);
            result.lightningBoltWidth = state.lightningBoltWidthMultiplier;
            result.stormPulseIntensity = math.lerp(0.58f, 1f, electricalActivity);
            result.shouldTriggerLightning = 1;
            result.shouldTriggerLightningStormPulse = 1;
        }

        private static float ResolveAupThunderDistanceMeters(float3 listenerPosition, float3 strikePosition, double3 absoluteUniverseOffset)
        {
            double3 listenerAbsolute = new double3(listenerPosition.x, listenerPosition.y, listenerPosition.z) + absoluteUniverseOffset;
            double3 strikeAbsolute = new double3(strikePosition.x, strikePosition.y, strikePosition.z) + absoluteUniverseOffset;
            return ApproximateDistanceMeters(strikeAbsolute - listenerAbsolute);
        }

        private static float ApproximateDistanceMeters(double3 delta)
        {
            double3 absolute = math.abs(delta);
            double maxAxis = math.max(absolute.x, math.max(absolute.y, absolute.z));
            if (!math.isfinite(maxAxis) || maxAxis <= 0d)
                return 0f;

            double minAxis = math.min(absolute.x, math.min(absolute.y, absolute.z));
            double midAxis = (absolute.x + absolute.y + absolute.z) - maxAxis - minAxis;
            double approximateDistance = maxAxis + (midAxis * 0.375d) + (minAxis * 0.25d);
            return (float)math.min(approximateDistance, (double)float.MaxValue);
        }

        private static float ResolveStormInterference(float precipitationIntensity, float electricalActivity, float threshold)
        {
            if (electricalActivity <= threshold)
                return 0f;

            float electricalT = math.saturate((electricalActivity - threshold) / math.max(1f - threshold, 0.0001f));
            float precipitationT = math.lerp(0.7f, 1f, math.saturate(precipitationIntensity));
            return electricalT * precipitationT;
        }

        private static float ResolveGustMultiplier(in SurfaceWeatherMathState state, float unscaledTime, float gustTimeOffset)
        {
            float gustStrength = math.saturate(state.gustStrength);
            if (gustStrength <= 0.001f)
                return 1f;

            float frequency = math.clamp(state.gustFrequency, 0.005f, 0.2f);
            float phase = (unscaledTime + gustTimeOffset) * frequency * TwoPi;
            float composite =
                CinematicMath.FastSin(phase) * 0.58f +
                CinematicMath.FastSin(phase * 0.43f + 1.17f) * 0.29f +
                CinematicMath.FastSin(phase * 1.73f + 0.41f) * 0.13f;
            float normalized = math.saturate((composite + 1f) * 0.5f);
            float envelope = normalized * normalized;
            float calmFloor = 1f - gustStrength * 0.12f;
            float gustPeak = 1f + gustStrength * 0.42f;
            return math.lerp(calmFloor, gustPeak, envelope);
        }

        private static float ResolveSquallMultiplier(in SurfaceWeatherMathState state, float unscaledTime, float gustTimeOffset)
        {
            float squallStrength = math.saturate(state.squallStrength);
            if (squallStrength <= 0.001f)
                return 1f;

            float frequency = math.clamp(state.squallFrequency, 0.005f, 0.08f);
            float phase = (unscaledTime + gustTimeOffset * 0.37f) * frequency * TwoPi;
            float composite =
                CinematicMath.FastSin(phase) * 0.61f +
                CinematicMath.FastSin(phase * 0.31f + 2.14f) * 0.27f +
                CinematicMath.FastSin(phase * 1.09f + 0.63f) * 0.12f;
            float normalized = math.saturate((composite + 1f) * 0.5f);
            float bandEnvelope = normalized * normalized * normalized;
            float calmFloor = 1f - squallStrength * 0.26f;
            float squallPeak = 1f + squallStrength * 0.72f;
            return math.lerp(calmFloor, squallPeak, bandEnvelope);
        }

        private static float2 RotateDirection(float2 direction, float angleRadians)
        {
            float sinValue = CinematicMath.FastSin(angleRadians);
            float cosValue = CinematicMath.FastCos(angleRadians);
            return new float2(
                direction.x * cosValue - direction.y * sinValue,
                direction.x * sinValue + direction.y * cosValue);
        }

        private static float NextRandom01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
