#ifndef HECTON_DITHER_FOG_INCLUDED
#define HECTON_DITHER_FOG_INCLUDED

CBUFFER_START(H8BiomeLightingParameters)
    float4 _H8BiomePrimaryFogColor;
    float4 _H8BiomeSecondaryFogColor;
    float _H8BiomeFogDensity;
    float _H8BiomeBlendFactor;
    float _H8BiomeLightShaftIntensity;
    float _H8GlobalQualityWeight;
    float4 _H8BiomePad1;
CBUFFER_END

CBUFFER_START(H8BiomeTransitionPayload)
    float4 _H8BiomePayloadFogColor;
    float4 _H8BiomePayloadAbsorptionParams;
    float4 _H8BiomePayloadAudioParams;
    float4 _H8BiomePayloadNormalizedWeights;
    float4 _H8BiomePayloadBiomeHashes;
    float4 _H8BiomePayloadDitherParams;
    float4 _H8BiomePayloadFrameFlags;
    float4 _H8BiomePayloadReserved0;
CBUFFER_END

float H8DitherFogFiniteOr(float value, float fallbackValue)
{
    return isfinite(value) ? value : fallbackValue;
}

float H8DitherFogSaturateFinite(float value)
{
    return saturate(H8DitherFogFiniteOr(value, 0.0));
}

float3 H8DitherFogColorFiniteOr(float3 value, float3 fallbackValue)
{
    return all(isfinite(value)) ? value : fallbackValue;
}

float H8DitherFogSmooth01(float value)
{
    float x = H8DitherFogSaturateFinite(value);
    return x * x * (3.0 - 2.0 * x);
}

float H8DitherFogSmoothRange01(float lower, float upper, float value)
{
    float width = max(upper - lower, 0.0001);
    return H8DitherFogSmooth01((value - lower) * rcp(width));
}

float H8DitherFogFastNegativeExp(float value)
{
    value = max(0.0, H8DitherFogFiniteOr(value, 0.0));
    float value2 = value * value;
    return rcp(1.0 + value + 0.48 * value2 + 0.235 * value2 * value);
}

float3 H8DitherFogSafeNormalize(float3 value, float3 fallbackValue)
{
    value = all(isfinite(value)) ? value : fallbackValue;
    float lengthSq = dot(value, value);
    return lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallbackValue;
}

float H8DitherFogBayer8x8(float2 pixelCoord)
{
    uint2 cell = (uint2)floor(abs(pixelCoord)) & 7u;
    uint index = cell.x + cell.y * 8u;
    const float thresholds[64] =
    {
        0.0078125, 0.7578125, 0.1953125, 0.9453125, 0.0546875, 0.8046875, 0.2421875, 0.9921875,
        0.5078125, 0.2578125, 0.6953125, 0.4453125, 0.5546875, 0.3046875, 0.7421875, 0.4921875,
        0.1328125, 0.8828125, 0.0703125, 0.8203125, 0.1796875, 0.9296875, 0.1171875, 0.8671875,
        0.6328125, 0.3828125, 0.5703125, 0.3203125, 0.6796875, 0.4296875, 0.6171875, 0.3671875,
        0.0390625, 0.7890625, 0.2265625, 0.9765625, 0.0234375, 0.7734375, 0.2109375, 0.9609375,
        0.5390625, 0.2890625, 0.7265625, 0.4765625, 0.5234375, 0.2734375, 0.7109375, 0.4609375,
        0.1640625, 0.9140625, 0.1015625, 0.8515625, 0.1484375, 0.8984375, 0.0859375, 0.8359375,
        0.6640625, 0.4140625, 0.6015625, 0.3515625, 0.6484375, 0.3984375, 0.5859375, 0.3359375
    };
    return thresholds[index];
}

float H8DitherFogHash21(float2 value)
{
    value = all(isfinite(value)) ? value : float2(0.0, 0.0);
    float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
    hash += dot(hash, hash.yzx + 33.33);
    return frac((hash.x + hash.y) * hash.z);
}

float H8DitherFogValueNoise(float2 value)
{
    float2 i = floor(value);
    float2 f = frac(value);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = H8DitherFogHash21(i);
    float b = H8DitherFogHash21(i + float2(1.0, 0.0));
    float c = H8DitherFogHash21(i + float2(0.0, 1.0));
    float d = H8DitherFogHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float H8DitherFogAnalyticalFactor(float linearDepth, float density, float qualityWeight, float ditherStrength, float2 pixelCoord)
{
    float safeDepth = max(0.0, H8DitherFogFiniteOr(linearDepth, 0.0));
    float safeDensity = max(0.0, H8DitherFogFiniteOr(density, 0.0));
    float baseFog = saturate(1.0 - H8DitherFogFastNegativeExp(safeDepth * safeDensity));
    float ditherWeight = H8DitherFogSmoothRange01(0.08, 0.72, qualityWeight) * H8DitherFogSaturateFinite(ditherStrength);
    float dither = (H8DitherFogBayer8x8(pixelCoord) - 0.5) * (1.0 / 64.0);
    return saturate(baseFog + dither * ditherWeight);
}

float4 H8DitherFogResolveBiomeFogColorAndDensity(float4 fallbackColorAndDensity)
{
    float payloadValid = step(0.000001, abs(_H8BiomePayloadFogColor.w) + abs(_H8BiomePayloadAbsorptionParams.w));
    float compactValid = step(0.5, abs(_H8BiomePad1.x));
    float4 payload = float4(_H8BiomePayloadFogColor.rgb, max(0.0, _H8BiomePayloadAbsorptionParams.w * 0.04));
    float4 compact = float4(
        lerp(_H8BiomePrimaryFogColor.rgb, _H8BiomeSecondaryFogColor.rgb, H8DitherFogSaturateFinite(_H8BiomeBlendFactor)),
        max(0.0, H8DitherFogFiniteOr(_H8BiomeFogDensity, 0.0)));
    float4 resolved = lerp(fallbackColorAndDensity, payload, payloadValid);
    resolved = lerp(resolved, compact, compactValid);
    resolved.rgb = max(H8DitherFogColorFiniteOr(resolved.rgb, float3(0.0015, 0.0023, 0.0031)), float3(0.0015, 0.0023, 0.0031));
    resolved.w = max(0.0, H8DitherFogFiniteOr(resolved.w, fallbackColorAndDensity.w));
    return resolved;
}

float H8DitherFogResolveDitherStrength(float fallbackStrength)
{
    float payloadStrength = H8DitherFogSaturateFinite(_H8BiomePayloadDitherParams.x);
    float payloadValid = step(0.000001, abs(_H8BiomePayloadDitherParams.x) + abs(_H8BiomePayloadDitherParams.w));
    return lerp(H8DitherFogSaturateFinite(fallbackStrength), payloadStrength, payloadValid);
}

float H8DitherFogResolveQualityWeight(float fallbackQuality)
{
    float fallback = H8DitherFogSaturateFinite(fallbackQuality);
    float global = H8DitherFogSaturateFinite(_H8GlobalQualityWeight);
    float payload = H8DitherFogSaturateFinite(_H8BiomePayloadDitherParams.w);
    float globalValid = step(0.5, abs(_H8BiomePad1.x));
    float payloadValid = step(0.000001, abs(_H8BiomePayloadDitherParams.w));
    float resolved = lerp(fallback, payload, payloadValid);
    return lerp(resolved, global, globalValid);
}

float H8DitherFogLightShaftOcclusion(float2 screenUv, float3 viewRayWS, float3 lightVectorWS, float linearDepth, float timeSeconds, float intensity, float qualityWeight)
{
    float3 viewDir = H8DitherFogSafeNormalize(viewRayWS, float3(0.0, 0.0, 1.0));
    float3 lightDir = H8DitherFogSafeNormalize(lightVectorWS, float3(0.0, 1.0, 0.0));
    float alignment = saturate(dot(-viewDir, lightDir) * 0.5 + 0.5);
    float particulate = H8DitherFogValueNoise(screenUv * lerp(24.0, 96.0, H8DitherFogSmooth01(qualityWeight)) + timeSeconds * float2(0.031, -0.017));
    float dither = H8DitherFogBayer8x8(screenUv * _ScreenParams.xy + floor(timeSeconds * 8.0));
    float particulateGate = lerp(1.0, step(dither, particulate), H8DitherFogSmoothRange01(0.18, 0.88, qualityWeight));
    float depthFalloff = H8DitherFogFastNegativeExp(max(0.0, linearDepth) * lerp(0.006, 0.018, H8DitherFogSmooth01(qualityWeight)));
    return H8DitherFogSaturateFinite(intensity) * alignment * particulateGate * depthFalloff;
}

float H8DitherFogSiltAlpha(float2 screenUv, float linearDepth, float cameraDistance, float timeSeconds, float density, float qualityWeight)
{
    float q = H8DitherFogSmoothRange01(0.12, 0.82, qualityWeight);
    float cheapLayer = H8DitherFogValueNoise(screenUv * 32.0 + timeSeconds * float2(0.011, 0.019));
    float richLayer = H8DitherFogValueNoise(screenUv * 73.0 + timeSeconds * float2(-0.027, 0.013));
    float noise = lerp(cheapLayer, cheapLayer * 0.68 + richLayer * 0.32, q);
    float softCameraFade = H8DitherFogSmooth01(saturate(cameraDistance * rcp(1.25)));
    float depthFade = saturate(1.0 - H8DitherFogFastNegativeExp(max(0.0, linearDepth) * 0.025));
    return saturate(noise * max(0.0, density) * softCameraFade * depthFade);
}

float H8DitherFogCausticDepthFade(float worldY, float shallowY, float deepY)
{
    float range = max(abs(shallowY - deepY), 0.001);
    return H8DitherFogSmooth01(saturate((worldY - deepY) * rcp(range)));
}

float2 H8DitherFogThermalDistortionOffset(float2 screenUv, float timeSeconds, float intensity, float qualityWeight)
{
    float q = H8DitherFogSmoothRange01(0.20, 0.92, qualityWeight);
    float triA = abs(frac(screenUv.y * 11.0 + timeSeconds * 0.19) * 2.0 - 1.0);
    float triB = abs(frac(screenUv.x * 7.0 - timeSeconds * 0.13) * 2.0 - 1.0);
    float2 offset = float2(triA - 0.5, triB - 0.5) * max(0.0, intensity) * lerp(0.0015, 0.009, q);
    return all(isfinite(offset)) ? offset : float2(0.0, 0.0);
}

#endif
