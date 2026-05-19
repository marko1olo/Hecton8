#ifndef HECTON_OCEAN_SURFACE_ATMOSPHERE_INCLUDED
#define HECTON_OCEAN_SURFACE_ATMOSPHERE_INCLUDED

#define H8_OCEAN_TWO_PI 6.28318530718

struct H8WaveParametersDTO
{
    float4 DirectionAndSteepness;
    float PhaseSpeed;
    float Amplitude;
    float Wavelength;
    uint Pad0;
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
CBUFFER_END

float H8OceanDesiredWaveCount(float qualityWeight)
{
    float q = saturate(qualityWeight);
    float normalized = saturate((q - 0.1) / 0.9);
    float qualityCurve = normalized * normalized * (3.0 - 2.0 * normalized);
    qualityCurve *= step(0.1, q);
    return lerp(4.0, 16.0, qualityCurve);
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
    float safePhase = ((phase == phase) && abs(phase) < 1.0e20) ? phase : 0.0;
    return safePhase - floor(safePhase / H8_OCEAN_TWO_PI) * H8_OCEAN_TWO_PI;
}

float H8OceanWrappedPhase(float2 cameraLocalXZ, float2 direction, float wavelength, float phaseOffset, float phaseSpeed)
{
    float safeWavelength = max(abs(wavelength), 0.25);
    float projected = dot(cameraLocalXZ, direction);
    float wrappedMeters = projected - floor(projected / safeWavelength) * safeWavelength;
    return H8OceanWrapPhase(wrappedMeters * (H8_OCEAN_TWO_PI / safeWavelength) + phaseOffset + phaseSpeed * _H8OceanSurfaceTime);
}

float2 H8OceanResolveAupProjectedXZ(float2 cameraLocalXZ)
{
    return cameraLocalXZ + _H8OceanCameraAupLocalProjection.xy;
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
    float2 projectedAupXZ = H8OceanResolveAupProjectedXZ(cameraLocalXZ);

    [loop]
    for (int i = 0; i < 16; i++)
    {
        float contribution = H8OceanWaveContribution(i, _H8OceanGlobalQualityWeight);
        if (contribution <= 0.0001 || i >= _H8OceanActiveWaveCount + 1)
            continue;

        H8WaveParametersDTO wave = _H8OceanWaveParameters[i];
        float2 direction = H8OceanNormalize2(wave.DirectionAndSteepness.xy, float2(1.0, 0.0));
        float wavelength = max(abs(wave.Wavelength), 0.25);
        float waveNumber = H8_OCEAN_TWO_PI / wavelength;
        float phase = H8OceanWrappedPhase(projectedAupXZ, direction, wavelength, wave.DirectionAndSteepness.z, wave.PhaseSpeed);
        float sine;
        float cosine;
        sincos(phase, sine, cosine);

        float amp = max(0.0, wave.Amplitude) * contribution;
        displacement.y += amp * sine;

        float slope = amp * waveNumber * cosine;
        dHeightDx += slope * direction.x;
        dHeightDz += slope * direction.y;

        float steepness = saturate(wave.DirectionAndSteepness.w);
        float horizontal = steepness * amp * cosine;
        displacement.xz += direction * horizontal;
        minJacobian = min(minJacobian, 1.0 - steepness * amp * waveNumber * sine);
    }

    normal = H8OceanNormalize3(float3(-dHeightDx, 1.0, -dHeightDz), float3(0.0, 1.0, 0.0));
    float foamThreshold = saturate(_H8OceanRainDisturbance.z);
    float qualityFoam = saturate((_H8OceanGlobalQualityWeight - 0.28) / 0.72) * step(0.28, _H8OceanGlobalQualityWeight);
    foamScalar = saturate((foamThreshold - minJacobian) * 4.0) * qualityFoam;
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
    scatter *= _H8OceanScatteringParams.x * lerp(0.5, 1.0, _H8OceanGlobalQualityWeight);
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
