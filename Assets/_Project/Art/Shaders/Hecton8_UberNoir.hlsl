#ifndef HECTON8_UBER_NOIR_INCLUDED
#define HECTON8_UBER_NOIR_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define H8_UBER_NOIR_PI 3.14159265359
#define H8_UBER_NOIR_EPS 0.0001
#define H8_UBER_NOIR_POM_STEPS 16

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_RustDetailMap);
SAMPLER(sampler_RustDetailMap);
TEXTURE2D(_BlueNoiseTex);
SAMPLER(sampler_BlueNoiseTex);
TEXTURE2D(_HectonCausticsMap);
SAMPLER(sampler_HectonCausticsMap);

struct H8UberNoirInstanceData
{
    float4x4 ObjectToWorld;
    float4x4 WorldToObject;
    float4 SeedFadeFlags; // x=seed, y=fade01, z=feature flags, w=reserved
};

StructuredBuffer<H8UberNoirInstanceData> _H8UberNoirInstanceData;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _RustDetailMap_ST;
    float4 _BaseColor;
    float4 _EmissionColor;
    float4 _RustTint;
    float4 _RustPitTint;
    float4 _BiolumLowColor;
    float4 _BiolumHighColor;
    float4 _NoirAbyssFloorColor;
    float4 _NoirFogColor;
    float4 _UberNoirCausticColor;
    float4 _UberNoirFeatureFlags;    // x=POM, y=caustics, z=bending, w=dither transparency
    float4 _UberNoirInstanceParams;  // x=buffer offset, y=buffer count, z=use buffer, w=seed bias
    float4 _UberNoirParallaxParams;  // x=scale, y=min view z, z=height bias, w=reserved
    float4 _UberNoirRustParams;      // x=strength, y=POM threshold, z=normal strength, w=wet smoothness
    float4 _UberNoirBendParams;      // x=local strength, y=grid scale, z=panel bow, w=low scar
    float4 _UberNoirCausticParams;   // x=intensity, y=max depth, z=shadow weight, w=refraction offset
    float4 _UberNoirBiolumParams;    // x=intensity, y=spectral shift, z=pulse sharpness, w=seed scale
    float4 _UberNoirDitherParams;    // x=cutoff, y=fog alpha, z=temporal strength, w=alpha scale
    float4 _UberNoirLightingParams;  // x=specular, y=roughness floor, z=ambient, w=emission scale
    float _Metallic;
    float _Smoothness;
    float _OcclusionStrength;
    float _BumpScale;
    float _Cutoff;
    float _NoirFogAlpha;
    float _UberNoirPadding0;
    float _UberNoirPadding1;
CBUFFER_END

// Frame/runtime globals. These are uploaded once by system owners, not per-material mutation.
#ifndef HECTON_CORE_LIT_INCLUDED
float4 _TotalUniverseOffset;
float4 _BiolumMasterPhase;
float4 _HectonProjectedCausticsWorldRect;
float4 _HectonProjectedCausticsParams;
float4 _HectonProjectedCausticsColor;
float4 _HectonCausticsRuntimeParams;
float4 _HectonCausticsSimulationParamsA;
float4 _HectonCausticsSimulationParamsB;
float4 _HectonCausticsSimulationParamsC;
float4 _HectonSubmarineCrushCenterRadius;
float4 _HectonSubmarineCrushDepthParams;
float4 _HectonHabitatStressCenterRadius;
float4 _HectonHabitatStressParams;
float4 _HectonMaterialDecayRuntime;
float4 _HectonPlayerBloodSplatter;
float _HectonEquipmentRust01;
#endif

struct H8UberNoirAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct H8UberNoirVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    half3 viewDirWS : TEXCOORD3;
    float2 uv : TEXCOORD4;
    half fogFactor : TEXCOORD5;
    half instanceSeed : TEXCOORD6;
    half instanceFade : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct H8UberNoirSurface
{
    half3 albedo;
    half3 normalWS;
    half3 emission;
    half metallic;
    half smoothness;
    half roughness;
    half occlusion;
    half alpha;
    half rustMask;
    half4 orm;
};

float H8UberNoirSafeRcp(float value)
{
    return rcp(max(abs(value), H8_UBER_NOIR_EPS));
}

float H8UberNoirSafeRsqrt(float value)
{
    return rsqrt(max(abs(value), H8_UBER_NOIR_EPS));
}

float H8UberNoirSafePow(float value, float exponent)
{
    float safeValue = max(value, H8_UBER_NOIR_EPS);
    float safeExponent = max(exponent, H8_UBER_NOIR_EPS);
    return pow(safeValue, safeExponent);
}

float H8UberNoirSafePow01(float value, float exponent)
{
    return saturate(H8UberNoirSafePow(saturate(value), exponent));
}

float3 H8UberNoirFinite3(float3 value, float3 fallbackValue)
{
    return all(isfinite(value)) ? value : fallbackValue;
}

float3 H8UberNoirSafeNormalize(float3 value, float3 fallbackValue)
{
    float lenSq = dot(value, value);
    if (!isfinite(lenSq) || lenSq <= H8_UBER_NOIR_EPS)
        return fallbackValue;

    return value * H8UberNoirSafeRsqrt(lenSq);
}

half3 H8UberNoirSafeNormalizeHalf(half3 value, half3 fallbackValue)
{
    half lenSq = dot(value, value);
    if (!isfinite(lenSq) || lenSq <= (half)H8_UBER_NOIR_EPS)
        return fallbackValue;

    return value * (half)H8UberNoirSafeRsqrt((float)lenSq);
}

float H8UberNoirTriangle01(float value)
{
    return 1.0 - abs(frac(value) * 2.0 - 1.0);
}

float H8UberNoirHash12(float2 value)
{
    float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
    hash += dot(hash, hash.yzx + 33.33);
    return frac((hash.x + hash.y) * hash.z);
}

float H8UberNoirValueNoise2(float2 value)
{
    float2 cell = floor(value);
    float2 local = frac(value);
    float2 smoothValue = local * local * (3.0 - 2.0 * local);
    float a = H8UberNoirHash12(cell);
    float b = H8UberNoirHash12(cell + float2(1.0, 0.0));
    float c = H8UberNoirHash12(cell + float2(0.0, 1.0));
    float d = H8UberNoirHash12(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, smoothValue.x), lerp(c, d, smoothValue.x), smoothValue.y);
}

float2 H8UberNoirScreenUV(float4 positionCS)
{
    float2 screenUV = positionCS.xy * rcp(max(abs(positionCS.w), H8_UBER_NOIR_EPS)) * 0.5 + 0.5;
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
#endif
    return saturate(screenUV);
}

half H8UberNoirBlueNoise(float4 positionCS)
{
    float2 screenUV = H8UberNoirScreenUV(positionCS);
    float2 r2 = frac(_Time.y * float2(0.75487766, 0.56984029) * max(_UberNoirDitherParams.z, 0.0));
    return SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, screenUV * (_ScaledScreenParams.xy * (1.0 / 64.0)) + r2).r;
}

void H8UberNoirClipDitheredTransparency(half alpha, float4 positionCS)
{
    half featureMask = (half)step(0.5, _UberNoirFeatureFlags.w);
    half threshold = lerp((half)_Cutoff, H8UberNoirBlueNoise(positionCS), featureMask);
    half coverage = saturate(alpha * (half)max(_UberNoirDitherParams.w, 0.0));
    clip(coverage - threshold);
}

H8UberNoirInstanceData H8UberNoirLoadInstance(uint instanceID)
{
    H8UberNoirInstanceData instanceData;
#if defined(H8_UBERNOIR_USE_INSTANCE_BUFFER)
    uint bufferCount = (uint)max(_UberNoirInstanceParams.y, 0.0);
    uint bufferOffset = (uint)max(_UberNoirInstanceParams.x, 0.0);
    uint clampedId = min(instanceID, max(bufferCount, 1u) - 1u);
    instanceData = _H8UberNoirInstanceData[bufferOffset + clampedId];
#else
    instanceData.ObjectToWorld = GetObjectToWorldMatrix();
    instanceData.WorldToObject = GetWorldToObjectMatrix();
    instanceData.SeedFadeFlags = float4(0.0, 1.0, 0.0, 0.0);
#endif
    return instanceData;
}

float4x4 H8UberNoirObjectToAupWorld(float4x4 objectToWorld)
{
    float3 universeOffset = H8UberNoirFinite3(_TotalUniverseOffset.xyz, float3(0.0, 0.0, 0.0));
    objectToWorld._m03 -= universeOffset.x;
    objectToWorld._m13 -= universeOffset.y;
    objectToWorld._m23 -= universeOffset.z;
    return objectToWorld;
}

float3 H8UberNoirTransformNormal(float3 normalOS, float4x4 worldToObject)
{
    float3 normalWS = mul(normalOS, (float3x3)worldToObject);
    return H8UberNoirSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
}

void H8UberNoirBuildTangentFrame(
    half3 normalWS,
    half4 tangentWS,
    out float3 safeNormalWS,
    out float3 safeTangentWS,
    out float3 safeBitangentWS)
{
    safeNormalWS = H8UberNoirSafeNormalize((float3)normalWS, float3(0.0, 1.0, 0.0));
    safeTangentWS = H8UberNoirSafeNormalize((float3)tangentWS.xyz, float3(1.0, 0.0, 0.0));
    safeBitangentWS = H8UberNoirSafeNormalize(cross(safeNormalWS, safeTangentWS) * tangentWS.w, float3(0.0, 0.0, 1.0));
}

float H8UberNoirBucklingMask(float3 positionWS, half instanceSeed)
{
    float gridScale = max(_UberNoirBendParams.y, H8_UBER_NOIR_EPS);
    float3 stablePosition = (positionWS + H8UberNoirFinite3(_TotalUniverseOffset.xyz, float3(0.0, 0.0, 0.0))) * gridScale;
    float2 cellA = floor(stablePosition.xz + instanceSeed * 17.0);
    float2 cellB = floor(stablePosition.xy * 1.37 + instanceSeed * 29.0);
    float panelA = H8UberNoirTriangle01(dot(cellA, float2(0.31, 0.47)));
    float panelB = H8UberNoirTriangle01(dot(cellB, float2(0.23, 0.41)));
    float crease = H8UberNoirTriangle01(dot(stablePosition, float3(0.019, 0.031, 0.043)));
    return saturate(panelA * 0.52 + panelB * 0.34 + crease * 0.28);
}

float H8UberNoirRadiusMask(float3 positionWS, float4 centerRadius)
{
    float radius = max(centerRadius.w, 0.0);
    float3 delta = positionWS - centerRadius.xyz;
    float radiusSq = max(radius * radius, H8_UBER_NOIR_EPS);
    return lerp(1.0, 1.0 - saturate(dot(delta, delta) * rcp(radiusSq)), step(H8_UBER_NOIR_EPS, radius));
}

float3 H8UberNoirApplyDynamicHullBendingWS(float3 positionWS, float3 normalWS, half instanceSeed)
{
#if defined(_MATH_LOD_LOW)
    return positionWS;
#else
    float featureMask = step(0.5, _UberNoirFeatureFlags.z);
    float localStrength = max(_UberNoirBendParams.x, 0.0);
    float crushDepth = max(_HectonSubmarineCrushDepthParams.y, H8_UBER_NOIR_EPS);
    float crush01 = saturate(max(_HectonSubmarineCrushDepthParams.x, 0.0) * rcp(crushDepth));
    float crushDisplacement = max(_HectonSubmarineCrushDepthParams.z, 0.0) * crush01;
    float crushMask = H8UberNoirRadiusMask(positionWS, _HectonSubmarineCrushCenterRadius);

    float habitatStress01 = saturate(_HectonHabitatStressParams.x);
    float habitatDisplacement = max(_HectonHabitatStressParams.y, 0.0) * habitatStress01;
    float habitatMask = 0.0;
    [branch]
    if (habitatDisplacement > H8_UBER_NOIR_EPS)
        habitatMask = H8UberNoirRadiusMask(positionWS, _HectonHabitatStressCenterRadius);

    float buckle = H8UberNoirBucklingMask(positionWS, instanceSeed) * 2.0 - 1.0;
    float displacement = (crushDisplacement * crushMask + habitatDisplacement * habitatMask) * buckle * localStrength * featureMask;
    return H8UberNoirFinite3(positionWS + H8UberNoirSafeNormalize(normalWS, float3(0.0, 1.0, 0.0)) * displacement, positionWS);
#endif
}

float2 H8UberNoirResolveRustPomUv(
    float2 rawUv,
    float2 baseUv,
    half3 viewDirWS,
    half3 normalWS,
    half4 tangentWS,
    out half4 rustPacked,
    out half rustMask)
{
    half rust01 = saturate((half)max(_HectonEquipmentRust01, _HectonMaterialDecayRuntime.x) * (half)max(_UberNoirRustParams.x, 0.0));
    float rustStValid = step(H8_UBER_NOIR_EPS, abs(_RustDetailMap_ST.x) + abs(_RustDetailMap_ST.y));
    float2 rustScale = lerp(float2(1.0, 1.0), _RustDetailMap_ST.xy, rustStValid);
    float2 rustOffset = _RustDetailMap_ST.zw * rustStValid;
    float2 rustUv = rawUv * rustScale + rustOffset;
    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, rustUv);
    rustMask = rust01;

#if defined(_MATH_LOD_LOW)
    return baseUv;
#else
    float pomEnabled = step(_UberNoirRustParams.y, rust01) * step(0.5, _UberNoirFeatureFlags.x) * step(_HectonMaterialDecayRuntime.z, 0.5);
    if (pomEnabled <= 0.0)
        return baseUv;

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    H8UberNoirBuildTangentFrame(normalWS, tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 safeViewWS = H8UberNoirSafeNormalize((float3)viewDirWS, safeNormalWS);
    float3 viewDirTS = float3(dot(safeViewWS, safeTangentWS), dot(safeViewWS, safeBitangentWS), max(dot(safeViewWS, safeNormalWS), max(_UberNoirParallaxParams.y, 0.16)));
    float viewInvZ = H8UberNoirSafeRcp(viewDirTS.z);
    float parallaxScale = max(_UberNoirParallaxParams.x, 0.0) * rust01;
    float2 parallaxStep = viewDirTS.xy * viewInvZ * parallaxScale * (1.0 / H8_UBER_NOIR_POM_STEPS);
    float2 resolvedUv = rustUv;
    float layerDepth = max(_UberNoirParallaxParams.z, 0.0);

    [unroll(H8_UBER_NOIR_POM_STEPS)]
    for (int stepIndex = 0; stepIndex < H8_UBER_NOIR_POM_STEPS; stepIndex++)
    {
        half sampledHeight = SAMPLE_TEXTURE2D_LOD(_RustDetailMap, sampler_RustDetailMap, resolvedUv, 0).r;
        half stepMask = (half)step(layerDepth, sampledHeight) * (half)pomEnabled;
        resolvedUv -= parallaxStep * stepMask;
        layerDepth += 1.0 / H8_UBER_NOIR_POM_STEPS;
    }

    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, resolvedUv);
    half pitMask = saturate((rustPacked.r - 0.34h) * 1.85h);
    rustMask = saturate(rust01 * lerp(0.58h, 1.0h, pitMask));
    float2 invRustScale = float2(H8UberNoirSafeRcp(rustScale.x), H8UberNoirSafeRcp(rustScale.y));
    float2 rawPomUv = rawUv + (resolvedUv - rustUv) * invRustScale;
    return rawPomUv * _BaseMap_ST.xy + _BaseMap_ST.zw;
#endif
}

half3 H8UberNoirDecodeRustNormalTS(half4 rustPacked, half strength)
{
    half2 xy = (rustPacked.gb * 2.0h - 1.0h) * saturate(strength);
    half z = (half)H8UberNoirSafeRsqrt(1.0h + dot(xy, xy));
    return half3(xy, z);
}

void H8UberNoirApplyRustCorrosion(
    float2 wearUv,
    float3 positionWS,
    half3 viewDirWS,
    half4 tangentWS,
    half4 rustPacked,
    half rustMask,
    inout H8UberNoirSurface surface)
{
#if defined(_MATH_LOD_LOW)
    return;
#else
    half finalRustMask = saturate(rustMask);
    if (finalRustMask > 0.0001h)
    {
        float3 safeNormalWS;
        float3 safeTangentWS;
        float3 safeBitangentWS;
        H8UberNoirBuildTangentFrame(surface.normalWS, tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
        half3 rustNormalTS = H8UberNoirDecodeRustNormalTS(rustPacked, finalRustMask * (half)_UberNoirRustParams.z);
        float3 rustNormalWS = H8UberNoirSafeNormalize(
            safeTangentWS * rustNormalTS.x + safeBitangentWS * rustNormalTS.y + safeNormalWS * rustNormalTS.z,
            safeNormalWS);
        surface.normalWS = (half3)H8UberNoirSafeNormalize(lerp(safeNormalWS, rustNormalWS, finalRustMask), safeNormalWS);

        half cavity = saturate((rustPacked.r - 0.42h) * 1.72h);
        surface.albedo = lerp(surface.albedo, (half3)_RustTint.rgb, finalRustMask * 0.62h);
        surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb, cavity * finalRustMask * 0.42h);
        surface.metallic = lerp(surface.metallic, 0.0h, finalRustMask);
        surface.smoothness = lerp(surface.smoothness, saturate(1.0h - rustPacked.a), finalRustMask);
        surface.roughness = saturate(1.0h - surface.smoothness);
    }

    half recentWet = saturate((half)_HectonMaterialDecayRuntime.y);
    surface.smoothness = lerp(surface.smoothness, saturate((half)_UberNoirRustParams.w), recentWet);
    surface.roughness = saturate(1.0h - surface.smoothness);

    half bloodActive = saturate((half)_HectonPlayerBloodSplatter.w);
    if (bloodActive > 0.0001h)
    {
        half bloodSource = saturate((half)max(_HectonPlayerBloodSplatter.x, _HectonPlayerBloodSplatter.y));
        half noiseA = (half)H8UberNoirHash12(floor(wearUv * 39.0 + _HectonMaterialDecayRuntime.w * 0.11));
        half patch = saturate((noiseA - 0.56h) * 2.65h) * bloodSource * bloodActive;
        surface.albedo = lerp(surface.albedo, (half3)_RustPitTint.rgb * 0.35h, patch * 0.72h);
        surface.smoothness = lerp(surface.smoothness, 1.0h, patch * saturate((half)_HectonPlayerBloodSplatter.z));
        surface.roughness = saturate(1.0h - surface.smoothness);
    }
#endif
}

half3 H8UberNoirResolveBiolumEmission(float3 positionWS, half emissionMask, half instanceSeed)
{
#if defined(_MATH_LOD_LOW)
    return half3(0.0h, 0.0h, 0.0h);
#else
    float phase01 = frac(_BiolumMasterPhase.x + _BiolumMasterPhase.y + instanceSeed * _UberNoirBiolumParams.w + dot(positionWS.xz, float2(0.013, -0.017)));
    half trianglePulse = (half)H8UberNoirTriangle01(phase01);
    half pulse = (half)H8UberNoirSafePow01(trianglePulse, max(_UberNoirBiolumParams.z, 0.25));
    half spectral = saturate(pulse + (pulse - 0.5h) * (half)_UberNoirBiolumParams.y);
    half3 spectralColor = lerp((half3)_BiolumLowColor.rgb, (half3)_BiolumHighColor.rgb, spectral);
    return spectralColor * (_EmissionColor.rgb * (half)_UberNoirBiolumParams.x * emissionMask);
#endif
}

float H8UberNoirEvaluateProceduralCaustics(float2 uv)
{
    float time = _Time.y + _HectonCausticsSimulationParamsB.z;
    float2 flowA = float2(_HectonCausticsSimulationParamsA.x, _HectonCausticsSimulationParamsA.y) * 0.001;
    float2 flowB = float2(_HectonCausticsSimulationParamsC.y, _HectonCausticsSimulationParamsC.z);
    float layerA = H8UberNoirValueNoise2(uv * 23.0 + time * (float2(0.031, -0.024) + flowA));
    float layerB = H8UberNoirValueNoise2(uv * 31.0 + time * (float2(-0.019, 0.037) + flowB));
    float sharpness = 1.0 + saturate(_HectonCausticsRuntimeParams.z) * 3.0;
    return H8UberNoirSafePow01(layerA * layerB, sharpness);
}

half3 H8UberNoirEvaluateAnalyticalCaustics(float3 positionWS, half3 normalWS, Light mainLight)
{
#if defined(_MATH_LOD_LOW)
    return half3(0.0h, 0.0h, 0.0h);
#else
    float featureMask = step(0.5, _UberNoirFeatureFlags.y) * step(H8_UBER_NOIR_EPS, _UberNoirCausticParams.x);
    float normalMask = saturate(normalWS.y);
    float2 uv = float2(
        (positionWS.x - _HectonProjectedCausticsWorldRect.x) * _HectonProjectedCausticsWorldRect.z,
        (positionWS.z - _HectonProjectedCausticsWorldRect.y) * _HectonProjectedCausticsWorldRect.w);
    uv += (float2)normalWS.xz * _UberNoirCausticParams.w;
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    float depthMeters = max(0.0, _HectonProjectedCausticsParams.y - positionWS.y);
    float depthFade = 1.0 - saturate(depthMeters * H8UberNoirSafeRcp(max(_UberNoirCausticParams.y, 1.0)));
    float attenuation = saturate(mainLight.distanceAttenuation * lerp(1.0, mainLight.shadowAttenuation, saturate(_UberNoirCausticParams.z)));
    float caustic = H8UberNoirEvaluateProceduralCaustics(uv);

    if (_HectonCausticsRuntimeParams.x > 0.5)
    {
        float3 sampled = SAMPLE_TEXTURE2D(_HectonCausticsMap, sampler_HectonCausticsMap, uv).rgb;
        caustic = dot(sampled, float3(0.27, 0.54, 0.19));
    }

    half intensity = (half)(featureMask * inside * depthFade * normalMask * attenuation * _UberNoirCausticParams.x * max(_HectonProjectedCausticsParams.x, 0.0));
    half3 tint = (half3)max(_HectonProjectedCausticsColor.rgb + _UberNoirCausticColor.rgb, _NoirAbyssFloorColor.rgb);
    return tint * (half)caustic * intensity;
#endif
}

H8UberNoirSurface H8UberNoirSampleSurface(H8UberNoirVaryings input)
{
    H8UberNoirSurface surface;
    float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
    float2 wearUv = baseUv;
    half4 rustPacked;
    half rustMask;

#if !defined(_MATH_LOD_LOW)
    wearUv = H8UberNoirResolveRustPomUv(input.uv, baseUv, input.viewDirWS, input.normalWS, input.tangentWS, rustPacked, rustMask);
#else
    rustPacked = half4(0.0h, 0.5h, 0.5h, 1.0h);
    rustMask = 0.0h;
#endif

    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, wearUv) * _BaseColor;
    half4 ormSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, wearUv);
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wearUv), (half)_BumpScale);

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    H8UberNoirBuildTangentFrame(input.normalWS, input.tangentWS, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 normalWS = H8UberNoirSafeNormalize(
        safeTangentWS * normalTS.x + safeBitangentWS * normalTS.y + safeNormalWS * normalTS.z,
        safeNormalWS);

    surface.albedo = baseSample.rgb;
    surface.normalWS = (half3)normalWS;
    surface.metallic = saturate(ormSample.r * (half)_Metallic);
    surface.occlusion = saturate(lerp(1.0h, ormSample.g, (half)_OcclusionStrength));
    surface.smoothness = saturate(ormSample.b * (half)_Smoothness);
    surface.roughness = max(saturate(1.0h - surface.smoothness), (half)_UberNoirLightingParams.y);
    surface.alpha = baseSample.a;
    surface.rustMask = rustMask;
    surface.orm = ormSample;
    surface.emission = _EmissionColor.rgb * ormSample.a * (half)_UberNoirLightingParams.w;

#if defined(_MATH_LOD_LOW)
    surface.normalWS = input.normalWS;
    surface.emission = half3(0.0h, 0.0h, 0.0h);
    surface.metallic = 0.0h;
    surface.smoothness = saturate(1.0h - surface.roughness);
    return surface;
#else
    H8UberNoirApplyRustCorrosion(wearUv, input.positionWS, input.viewDirWS, input.tangentWS, rustPacked, rustMask, surface);
    surface.emission += H8UberNoirResolveBiolumEmission(input.positionWS, ormSample.a, input.instanceSeed);
    return surface;
#endif
}

half3 H8UberNoirEvaluateMainLighting(H8UberNoirVaryings input, H8UberNoirSurface surface)
{
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 normalWS = H8UberNoirSafeNormalizeHalf(surface.normalWS, half3(0.0h, 1.0h, 0.0h));
    half3 viewDirWS = H8UberNoirSafeNormalizeHalf(input.viewDirWS, half3(0.0h, 0.0h, 1.0h));
    half3 lightDir = H8UberNoirSafeNormalizeHalf((half3)mainLight.direction, half3(0.0h, 1.0h, 0.0h));

    half nDotL = saturate(dot(normalWS, lightDir));
    half attenuation = saturate((half)(mainLight.distanceAttenuation * mainLight.shadowAttenuation));
    half attenuationGate = (half)step(0.0001h, nDotL) * (half)step(0.0001h, attenuation);
    half3 diffuse = surface.albedo * mainLight.color * (nDotL * attenuation * attenuationGate);

    half3 halfDir = H8UberNoirSafeNormalizeHalf(lightDir + viewDirWS, lightDir);
    half nDotH = saturate(dot(normalWS, halfDir));
    half specPower = lerp(4.0h, 64.0h, surface.smoothness);
    half specular = (half)H8UberNoirSafePow01(nDotH, specPower) * (half)_UberNoirLightingParams.x * attenuationGate;
    half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), surface.albedo, surface.metallic);
    half3 ambient = SampleSH(normalWS) * surface.albedo * surface.occlusion * (half)_UberNoirLightingParams.z;

#if defined(_MATH_LOD_LOW)
    return ambient + diffuse * lerp(0.55h, 1.0h, 1.0h - surface.roughness);
#else
    half3 caustics = H8UberNoirEvaluateAnalyticalCaustics(input.positionWS, normalWS, mainLight) * surface.albedo;
    return ambient + diffuse + f0 * specular + caustics + surface.emission;
#endif
}

half3 H8UberNoirApplyNoirFog(half3 color, half fogFactor)
{
    half fog = saturate(fogFactor * (half)max(_NoirFogAlpha, _UberNoirDitherParams.y));
    half fogCurve = fog * fog * (0.82h + fog * 0.18h);
    half3 floorColor = max((half3)_NoirFogColor.rgb, (half3)_NoirAbyssFloorColor.rgb);
    return lerp(color, floorColor, fogCurve);
}

half4 H8UberNoirFragment(H8UberNoirVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    H8UberNoirSurface surface = H8UberNoirSampleSurface(input);
    H8UberNoirClipDitheredTransparency(surface.alpha, input.positionCS);

    half3 color = H8UberNoirEvaluateMainLighting(input, surface);
    color = H8UberNoirApplyNoirFog(color, input.fogFactor);
    half3 abyssFloor = (half3)_NoirAbyssFloorColor.rgb;
    color = all(isfinite(color)) ? max(color, abyssFloor) : abyssFloor;
    return half4(color, 1.0h);
}

H8UberNoirVaryings H8UberNoirVertex(H8UberNoirAttributes input)
{
    H8UberNoirVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    uint resolvedInstanceID = input.instanceID;
#if UNITY_ANY_INSTANCING_ENABLED
    resolvedInstanceID = unity_InstanceID;
#endif

    H8UberNoirInstanceData instanceData = H8UberNoirLoadInstance(resolvedInstanceID);
    float4x4 objectToAupWorld = H8UberNoirObjectToAupWorld(instanceData.ObjectToWorld);
    float3 positionOS = H8UberNoirFinite3(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
    float3 normalOS = H8UberNoirSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
    float3 positionWS = mul(objectToAupWorld, float4(positionOS, 1.0)).xyz;
    float3 normalWS = H8UberNoirTransformNormal(normalOS, instanceData.WorldToObject);
    positionWS = H8UberNoirApplyDynamicHullBendingWS(positionWS, normalWS, (half)(instanceData.SeedFadeFlags.x + _UberNoirInstanceParams.w));

    float3 tangentWS = H8UberNoirSafeNormalize(mul((float3x3)objectToAupWorld, input.tangentOS.xyz), float3(1.0, 0.0, 0.0));
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = (half3)normalWS;
    output.tangentWS = half4((half3)tangentWS, input.tangentOS.w);
    output.viewDirWS = (half3)H8UberNoirSafeNormalize(GetWorldSpaceViewDir(positionWS), float3(0.0, 0.0, 1.0));
    output.uv = input.uv;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.instanceSeed = (half)saturate(frac(instanceData.SeedFadeFlags.x + _UberNoirInstanceParams.w));
    output.instanceFade = (half)saturate(instanceData.SeedFadeFlags.y);
    return output;
}

#endif
