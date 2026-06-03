#ifndef HECTON_HULL_BAKED_DISPLACEMENT_1722_INCLUDED
#define HECTON_HULL_BAKED_DISPLACEMENT_1722_INCLUDED

TEXTURE2D(_H8HullAlbedoMap);
SAMPLER(sampler_H8HullAlbedoMap);
TEXTURE2D(_H8HullMraoMap);
SAMPLER(sampler_H8HullMraoMap);
TEXTURE2D(_H8HullDisplacementMap);
SAMPLER(sampler_H8HullDisplacementMap);
TEXTURE2D(_H8HullCavitationFlipbook);
SAMPLER(sampler_H8HullCavitationFlipbook);

float4 _H8HullBakeParams;        // x=baked enable, y=displacement meters, z=scar blend, w=GlobalQualityWeight
float4 _H8HullBakeUvParams;      // xy=uv scale, zw=uv offset
float4 _H8HullCavitationParams;  // x=intensity, y=phase01, z=frame count, w=tiles per axis
float4 _H8HullCavitationUvParams; // xy=local UV scale, zw=local UV offset

float H8Hull1722SafeFinite(float value, float fallback)
{
    return isfinite(value) ? value : fallback;
}

float2 H8Hull1722SafeFinite2(float2 value, float2 fallback)
{
    return all(isfinite(value)) ? value : fallback;
}

float3 H8Hull1722SafeFinite3(float3 value, float3 fallback)
{
    return all(isfinite(value)) ? value : fallback;
}

float3 H8Hull1722SafeNormalize(float3 value, float3 fallback)
{
    float lenSq = dot(value, value);
    return (lenSq > 1.0e-8 && isfinite(lenSq)) ? value * rsqrt(lenSq) : fallback;
}

float H8Hull1722FeatureWeight(float featureMask)
{
    float enabled = saturate(H8Hull1722SafeFinite(_H8HullBakeParams.x, 0.0));
    return saturate(enabled * featureMask);
}

bool H8Hull1722IsBakedActive(float featureMask)
{
    return H8Hull1722FeatureWeight(featureMask) > 0.0001;
}

float2 H8Hull1722Uv(float2 uv)
{
    float2 scale = max(abs(_H8HullBakeUvParams.xy), float2(0.0001, 0.0001));
    float2 offset = H8Hull1722SafeFinite2(_H8HullBakeUvParams.zw, float2(0.0, 0.0));
    return H8Hull1722SafeFinite2(uv * scale + offset, uv);
}

float4 H8Hull1722SampleDisplacementLod(float2 uv, float lod)
{
    return SAMPLE_TEXTURE2D_LOD(_H8HullDisplacementMap, sampler_H8HullDisplacementMap, H8Hull1722Uv(uv), lod);
}

float4 H8Hull1722SampleDisplacement(float2 uv)
{
    return SAMPLE_TEXTURE2D(_H8HullDisplacementMap, sampler_H8HullDisplacementMap, H8Hull1722Uv(uv));
}

float H8Hull1722SignedDisplacement(float4 packed)
{
    float height01 = saturate(H8Hull1722SafeFinite(packed.r, 0.5));
    return (height01 - 0.5) * 2.0;
}

float H8Hull1722Scar01FromPacked(float4 packed)
{
    float inwardDent = saturate((0.5 - H8Hull1722SafeFinite(packed.r, 0.5)) * 2.0);
    float scar = saturate(H8Hull1722SafeFinite(packed.a, 0.0));
    return saturate(max(inwardDent, scar) * saturate(_H8HullBakeParams.z));
}

float3 H8Hull1722ApplyBakedDisplacementOS(float3 positionOS, float3 normalOS, float2 uv, float strength, float featureMask)
{
    float bakedWeight = H8Hull1722FeatureWeight(featureMask);
    if (bakedWeight <= 0.0001)
        return positionOS;

    float4 packed = H8Hull1722SampleDisplacementLod(uv, 0.0);
    float signedHeight = H8Hull1722SignedDisplacement(packed);
    float quality = saturate(H8Hull1722SafeFinite(_H8HullBakeParams.w, 1.0));
    float meters = max(_H8HullBakeParams.y, 0.0) * max(strength, 0.0) * lerp(0.45, 1.0, quality);
    float3 safeNormalOS = H8Hull1722SafeNormalize(normalOS, float3(0.0, 1.0, 0.0));
    float3 displaced = positionOS + safeNormalOS * (signedHeight * meters * bakedWeight);
    return H8Hull1722SafeFinite3(displaced, positionOS);
}

float3 H8Hull1722EvaluateBakedNormalBiasOS(float3 normalOS, float2 uv, float featureMask)
{
    float bakedWeight = H8Hull1722FeatureWeight(featureMask);
    if (bakedWeight <= 0.0001)
        return float3(0.0, 0.0, 0.0);

    float4 packed = H8Hull1722SampleDisplacementLod(uv, 0.0);
    float scar = H8Hull1722Scar01FromPacked(packed);
    float2 encodedSlope = saturate(packed.gb) * 2.0 - 1.0;
    float slopeMagnitude = saturate(length(encodedSlope));
    float quality = saturate(H8Hull1722SafeFinite(_H8HullBakeParams.w, 1.0));
    float bias = scar * slopeMagnitude * lerp(0.012, 0.045, quality) * bakedWeight;
    return H8Hull1722SafeNormalize(normalOS, float3(0.0, 1.0, 0.0)) * bias;
}

float H8Hull1722EvaluateBakedScar01(float2 uv, float featureMask)
{
    float bakedWeight = H8Hull1722FeatureWeight(featureMask);
    if (bakedWeight <= 0.0001)
        return 0.0;

    float4 packed = H8Hull1722SampleDisplacementLod(uv, 0.0);
    return H8Hull1722Scar01FromPacked(packed) * bakedWeight;
}

float4 H8Hull1722SampleAlbedo(float2 uv, float4 fallbackSample, float featureMask)
{
    float bakedWeight = H8Hull1722FeatureWeight(featureMask);
    if (bakedWeight <= 0.0001)
        return fallbackSample;

    float4 bakedSample = SAMPLE_TEXTURE2D(_H8HullAlbedoMap, sampler_H8HullAlbedoMap, H8Hull1722Uv(uv));
    float3 bakedRgb = H8Hull1722SafeFinite3(bakedSample.rgb, fallbackSample.rgb);
    float4 baked = float4(bakedRgb, fallbackSample.a);
    float blend = saturate(bakedWeight * lerp(0.65, 1.0, saturate(_H8HullBakeParams.w)));
    return lerp(fallbackSample, baked, blend);
}

float4 H8Hull1722SampleMrao(float2 uv, float4 fallbackSample, float featureMask)
{
    float bakedWeight = H8Hull1722FeatureWeight(featureMask);
    if (bakedWeight <= 0.0001)
        return fallbackSample;

    float4 packed = SAMPLE_TEXTURE2D(_H8HullMraoMap, sampler_H8HullMraoMap, H8Hull1722Uv(uv));
    packed = saturate(packed);
    float4 convertedForUberNoir = float4(packed.b, packed.g, packed.r, packed.a);
    return lerp(fallbackSample, convertedForUberNoir, bakedWeight);
}

float2 H8Hull1722CavitationAtlasUv(float2 localUv, out float frame01)
{
    float frames = max(H8Hull1722SafeFinite(_H8HullCavitationParams.z, 64.0), 1.0);
    float tiles = max(H8Hull1722SafeFinite(_H8HullCavitationParams.w, 8.0), 1.0);
    float frame = floor(frac(_H8HullCavitationParams.y) * frames);
    frame = min(frame, frames - 1.0);
    float tileY = floor(frame / tiles);
    float tileX = frame - tileY * tiles;
    frame01 = frames > 1.0 ? frame / (frames - 1.0) : 0.0;
    return (saturate(localUv) + float2(tileX, tileY)) / tiles;
}

float4 H8Hull1722SampleCavitationFoam(float2 localUv)
{
    float intensity = saturate(H8Hull1722SafeFinite(_H8HullCavitationParams.x, 0.0));
    if (intensity <= 0.0001)
        return float4(0.0, 0.0, 0.0, 0.0);

    localUv = H8Hull1722SafeFinite2(localUv, float2(-1.0, -1.0));
    float inside =
        step(0.0, localUv.x) *
        step(0.0, localUv.y) *
        step(localUv.x, 1.0) *
        step(localUv.y, 1.0);
    if (inside <= 0.0001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float frame01;
    float2 atlasUv = H8Hull1722CavitationAtlasUv(localUv, frame01);
    float4 foam = SAMPLE_TEXTURE2D(_H8HullCavitationFlipbook, sampler_H8HullCavitationFlipbook, atlasUv);
    foam = saturate(foam);
    foam.a *= intensity * inside;
    foam.rgb *= foam.a;
    return foam;
}

float4 H8Hull1722SampleCavitationFoamFromHullUv(float2 hullUv)
{
    float2 scale = H8Hull1722SafeFinite2(_H8HullCavitationUvParams.xy, float2(4.0, 2.0));
    float2 offset = H8Hull1722SafeFinite2(_H8HullCavitationUvParams.zw, float2(-3.0, -0.5));
    return H8Hull1722SampleCavitationFoam(hullUv * scale + offset);
}

#endif
