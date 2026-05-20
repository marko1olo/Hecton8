#ifndef HECTON_OCEAN_SURFACE_ATMOSPHERE_INCLUDED
#define HECTON_OCEAN_SURFACE_ATMOSPHERE_INCLUDED

#define H8_OCEAN_TWO_PI 6.28318530718

struct H8WaveParametersDTO
{
    float4 Wave1;
    float4 Wave2;
    float4 Wave3;
    float4 GlobalWindAndStorm;
};

StructuredBuffer<H8WaveParametersDTO> _H8OceanWaveParameters;

CBUFFER_START(HectonOceanSurfaceAtmosphere)
float _H8OceanSurfaceTime;
float _H8OceanGlobalQualityWeight;
int _H8OceanActiveWaveCount;
float _H8OceanPad0;
float4 _H8OceanWeather;
float4 _H8OceanRainDisturbance;
float4 _H8OceanRayleighBeta;
float4 _H8OceanMieBeta;
float4 _H8OceanScatteringParams;
float4 _H8OceanPlanetParams;
float4 _H8OceanRadialGridLod;
float4 _H8OceanCameraAupLocalProjection;
float4 _H8OceanWavePhaseBase0;
float4 _H8OceanWavePhaseBase1;
CBUFFER_END

float H8OceanFiniteOr(float value, float fallbackValue)
{
    return ((value == value) && abs(value) < 1.0e20) ? value : fallbackValue;
}

float H8OceanSafeQuality(float qualityWeight)
{
    return saturate(H8OceanFiniteOr(qualityWeight, 0.0));
}

float H8OceanDesiredWaveCount(float qualityWeight)
{
    float q = H8OceanSafeQuality(qualityWeight);
    float qualityCurve = q * q * (3.0 - 2.0 * q);
    return lerp(1.0, 6.0, qualityCurve);
}

float H8OceanWaveContribution(int waveIndex, float qualityWeight)
{
    return saturate(H8OceanDesiredWaveCount(qualityWeight) - (float)waveIndex);
}

float2 H8OceanNormalize2(float2 value, float2 fallbackValue)
{
    float lenSq = dot(value, value);
    return lenSq > 1e-6 ? value * rsqrt(lenSq) : fallbackValue;
}

float3 H8OceanNormalize3(float3 value, float3 fallbackValue)
{
    float lenSq = dot(value, value);
    return lenSq > 1e-6 ? value * rsqrt(lenSq) : fallbackValue;
}

float H8OceanWrapPhase(float phase)
{
    float safePhase = H8OceanFiniteOr(phase, 0.0);
    return safePhase - floor(safePhase / H8_OCEAN_TWO_PI) * H8_OCEAN_TWO_PI;
}

float H8OceanWrappedPhase(float2 cameraLocalXZ, float2 direction, float wavelength, float phaseBase, float phaseOffset, float phaseSpeed)
{
    float safeWavelength = max(abs(wavelength), 0.25);
    float projected = dot(cameraLocalXZ, direction);
    return H8OceanWrapPhase(phaseBase + projected * (H8_OCEAN_TWO_PI / safeWavelength) + phaseOffset + phaseSpeed * _H8OceanSurfaceTime);
}

float4 H8OceanGetWaveLane(H8WaveParametersDTO dto, int laneIndex)
{
    if (laneIndex == 0)
        return dto.Wave1;
    if (laneIndex == 1)
        return dto.Wave2;
    return dto.Wave3;
}

float H8OceanGetWavePhaseBase(int waveIndex)
{
    if (waveIndex == 0)
        return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase0.x, 0.0));
    if (waveIndex == 1)
        return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase0.y, 0.0));
    if (waveIndex == 2)
        return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase0.z, 0.0));
    if (waveIndex == 3)
        return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase0.w, 0.0));
    if (waveIndex == 4)
        return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase1.x, 0.0));
    return H8OceanWrapPhase(H8OceanFiniteOr(_H8OceanWavePhaseBase1.y, 0.0));
}

float2 H8OceanWaveDirection(float4 lane)
{
    float sine;
    float cosine;
    sincos(H8OceanFiniteOr(lane.x, 0.0), sine, cosine);
    return float2(cosine, sine);
}

float H8OceanWaveWavelength(float4 lane)
{
    return max(abs(H8OceanFiniteOr(lane.z, 0.25)), 0.25);
}

float H8OceanWaveSteepness(float4 lane)
{
    return saturate(H8OceanFiniteOr(lane.y, 0.0));
}

float H8OceanWaveSpeed(float4 lane)
{
    return H8OceanFiniteOr(lane.w, 0.0);
}

float H8OceanWaveAmplitude(float4 lane)
{
    float wavelength = H8OceanWaveWavelength(lane);
    float waveNumber = H8_OCEAN_TWO_PI / wavelength;
    return min(wavelength * 0.125, H8OceanWaveSteepness(lane) / max(waveNumber, 0.000001));
}

uint H8OceanHash(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return value;
}

float H8OceanHashNoise(float2 uv, float timeSeconds)
{
    uint2 cell = (uint2)floor(abs(uv) * 1024.0 + timeSeconds * float2(17.0, 31.0));
    uint h = H8OceanHash(cell.x ^ (cell.y * 0x9e3779b9u));
    return (float)(h & 0x00ffffffu) * (1.0 / 16777216.0);
}

void H8EvaluateOceanSurface(float2 cameraLocalXZ, out float3 displacement, out float3 normal, out float foamScalar)
{
    displacement = 0.0;
    float dHeightDx = 0.0;
    float dHeightDz = 0.0;
    float minJacobian = 1.0;
    float qualityWeight = H8OceanSafeQuality(_H8OceanGlobalQualityWeight);

    [loop]
    for (int i = 0; i < 6; i++)
    {
        float contribution = H8OceanWaveContribution(i, qualityWeight);
        if (contribution <= 0.0001 || i >= _H8OceanActiveWaveCount + 1)
            continue;

        H8WaveParametersDTO wave = _H8OceanWaveParameters[i / 3];
        int laneIndex = i - (i / 3) * 3;
        float4 lane = H8OceanGetWaveLane(wave, laneIndex);
        float2 direction = H8OceanWaveDirection(lane);
        float wavelength = H8OceanWaveWavelength(lane);
        float waveNumber = H8_OCEAN_TWO_PI / wavelength;
        float phaseOffset = (float)(i + 1) * 0.754877666;
        float phase = H8OceanWrappedPhase(cameraLocalXZ, direction, wavelength, H8OceanGetWavePhaseBase(i), phaseOffset, H8OceanWaveSpeed(lane));
        float sine;
        float cosine;
        sincos(phase, sine, cosine);

        float amp = H8OceanWaveAmplitude(lane) * contribution;
        displacement.y += amp * sine;

        float slope = amp * waveNumber * cosine;
        dHeightDx += slope * direction.x;
        dHeightDz += slope * direction.y;

        float steepness = H8OceanWaveSteepness(lane);
        float horizontal = steepness * amp * cosine;
        displacement.xz += direction * horizontal;
        minJacobian = min(minJacobian, 1.0 - steepness * amp * waveNumber * sine);
    }

    normal = H8OceanNormalize3(float3(-dHeightDx, 1.0, -dHeightDz), float3(0.0, 1.0, 0.0));
    float foamThreshold = saturate(_H8OceanRainDisturbance.z);
    float qualityFoam = saturate((qualityWeight - 0.28) / 0.72) * step(0.28, qualityWeight);
    foamScalar = saturate((foamThreshold - minJacobian) * 4.0) * qualityFoam;
    bool finiteSurface = all(displacement == displacement) && all(abs(displacement) < 1.0e20) &&
        all(normal == normal) && all(abs(normal) < 1.0e20) &&
        (foamScalar == foamScalar) && abs(foamScalar) < 1.0e20;
    if (!finiteSurface)
    {
        displacement = 0.0;
        normal = float3(0.0, 1.0, 0.0);
        foamScalar = 0.0;
    }
}

float3 H8EvaluateOceanAtmosphere(float3 viewDir, float sunDot)
{
    float rayleighPhase = 0.75 + 0.75 * sunDot * sunDot;
    float mieG = saturate(_H8OceanMieBeta.w);
    float mieDenom = max(0.05, 1.0 + mieG * mieG - 2.0 * mieG * sunDot);
    float miePhase = (1.0 - mieG * mieG) / (mieDenom * sqrt(mieDenom));
    float horizon = saturate(1.0 - abs(viewDir.y));
    float gasGiant = smoothstep(_H8OceanPlanetParams.x, _H8OceanPlanetParams.x + _H8OceanPlanetParams.y, horizon);
    float3 scatter = _H8OceanRayleighBeta.xyz * rayleighPhase + _H8OceanMieBeta.xyz * miePhase;
    scatter *= _H8OceanScatteringParams.x * lerp(0.5, 1.0, H8OceanSafeQuality(_H8OceanGlobalQualityWeight));
    scatter += gasGiant * _H8OceanScatteringParams.y * float3(0.42, 0.22, 0.58);
    return scatter;
}

float3 H8ApplyRainSurfaceDisturbance(float3 normalWS, float2 uv)
{
    float rain = saturate(_H8OceanRainDisturbance.x);
    float surge = saturate(_H8OceanRainDisturbance.y);
    float n = H8OceanHashNoise(uv, _H8OceanSurfaceTime);
    float ripple = (n - 0.5) * rain * lerp(0.03, 0.12, surge);
    return H8OceanNormalize3(normalWS + float3(ripple, 0.0, -ripple), normalWS);
}

#endif
