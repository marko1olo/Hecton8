#ifndef HECTON_CORE_LIT_INCLUDED
#define HECTON_CORE_LIT_INCLUDED

#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

#define HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_INPUT_INSTANCE_ID
#define HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO UNITY_VERTEX_OUTPUT_STEREO
#define HECTON_CORE_LIT_SETUP_INSTANCE_ID(input) UNITY_SETUP_INSTANCE_ID(input)
#define HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output) UNITY_TRANSFER_INSTANCE_ID(input, output)
#define HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output) UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output)
#define HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)

#ifndef HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS
#define HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS 7
#endif

#ifndef HECTON_PARASITE_MAX_ANCHORS
#define HECTON_PARASITE_MAX_ANCHORS 16
#endif

#ifndef HECTON_GLOW_POINT_MAX
#define HECTON_GLOW_POINT_MAX 16
#endif

#ifndef HECTON_HULL_DENT_MAX
#define HECTON_HULL_DENT_MAX 16
#endif

#ifndef HECTON_ACTIVE_SONAR_MAX_PINGS
#define HECTON_ACTIVE_SONAR_MAX_PINGS 4
#endif

#ifndef HECTON_CORE_LIT_HABITAT_STRESS_EPSILON
#define HECTON_CORE_LIT_HABITAT_STRESS_EPSILON 0.0025
#endif

#ifndef HECTON_CORE_LIT_HABITAT_DISPLACEMENT_EPSILON
#define HECTON_CORE_LIT_HABITAT_DISPLACEMENT_EPSILON 0.0001
#endif

float4 _HectonFlashlightPositionWS;
float4 _HectonFlashlightDirectionWS;
float4 _HectonFlashlightColor;
float4 _HectonFlashlightConeData;
float4 _HectonFlashlightVoxelHalfExtents;
float4x4 _HectonFlashlightVoxelWorldToLocal;
float _HectonFlashlightActive;
float _HectonFlashlightVoxelActive;
float _HectonFlashlightShadowSteps;
float _HectonFlashlightShadowSoftness;
float _HectonFlashlightShadowMinStep;
float _HectonFlashlightShadowBias;
float _HectonFlashlightShadowFloor;
float4 _HectonCaveVoxelHalfExtents;
float4 _HectonCaveVoxelInvDoubleHalfExtents;
float4x4 _HectonCaveVoxelWorldToLocal;
float4 _HectonCaveVoxelAoParams;
float4 _HectonBiolumVolumeHalfExtents;
float4 _HectonBiolumVolumeParams;
float4 _HectonGlowPointPositionRange[HECTON_GLOW_POINT_MAX];
float4 _HectonGlowPointColorIntensity[HECTON_GLOW_POINT_MAX];
float4 _HectonGlowPointParams; // x=count, y=sonar pulse gain, z/w=reserved
float4 _BrineColor;
float _BrineHeightY;
float4 _FinalGiantAbyssLight;
float4 _SunDirection;
float4 _AegirDirection;
float4 _H8AbyssAbsorptionColor;     // rgb=readability floor/tint, w=depth mask
float4 _H8AbyssAtmosphereParams;    // x=fog boost, y=detail weight, z=quality, w=depth meters
float4 _CausticOffset;              // xy=wrapped scroll, z=shallow mask, w=phase

#ifndef HECTON_CORE_LIT_VISUAL_CLOCK_SECONDS
#define HECTON_CORE_LIT_VISUAL_CLOCK_SECONDS 32.0
#endif

float HectonCoreLitResolveWrappedVisualTime()
{
    return frac(_CausticOffset.w) * HECTON_CORE_LIT_VISUAL_CLOCK_SECONDS;
}

float4 _HectonEclipseWaterShadowParams;    // xy=center xz, z=radius, w=darkening
float4 _HectonEclipseWaterShadowDirection; // xy=travel direction, z=softness, w=penumbra
float4 _HectonRingCausticsParams;          // x=strength, y=stripe scale, z=phase, w=softness
float4 _HectonRingCausticsDirection;       // xy=band direction, z=sun alignment, w=reserved
float4 _AbyssalFlowWeatherCurrent;
float4 _HectonPhotophobiaFieldOriginScale;
float4 _HectonPhotophobiaFieldState;
float4 _SonarRevealOriginWS;
float4 _SonarRevealWaveParams;
float4 _ActiveSonarCenterAUP;
float _ActiveSonarRadius;
float4 _ActiveSonarCentersRadius[HECTON_ACTIVE_SONAR_MAX_PINGS];
float4 _ActiveSonarParams[HECTON_ACTIVE_SONAR_MAX_PINGS];
float4 _ActiveSonarGeoParams; // x=count, y=max range, z=grid enabled, w=speed
float _HectonContactShadowStrength;
float _HectonContactShadowSteps;
float _HectonContactShadowBias;
float _HectonContactShadowMaxDistance;
float _HectonCaveVoxelActive;
float _HectonBiolumVolumeActive;
float _SonarWaveFront;
float _SonarRevealExpireTime;
float4x4 _HectonBiolumVolumeWorldToLocal;

TEXTURE3D(_VoxelDensityTex);
SAMPLER(sampler_VoxelDensityTex);
TEXTURE3D(_HectonCaveVoxelSdfTex);
SAMPLER(sampler_HectonCaveVoxelSdfTex);
TEXTURE3D(_HectonBiolumVolumeTex);
SAMPLER(sampler_HectonBiolumVolumeTex);
TEXTURE2D(_HectonPhotophobiaFieldTex);
SAMPLER(sampler_HectonPhotophobiaFieldTex);
TEXTURE2D(_HectonMicroNormalTex);
SAMPLER(sampler_HectonMicroNormalTex);
TEXTURE2D(_RustDetailMap);
SAMPLER(sampler_RustDetailMap);
float4 _RustDetailMap_ST;
#define HECTON_NOIR_FOG_LUT_SAMPLE_COUNT 16
float4 _NoirFogLUTSamples[HECTON_NOIR_FOG_LUT_SAMPLE_COUNT];
float4 _HectonNoirFogLutParams;
float _HectonNoirFogLutBlend;
float _HectonWeatherIntensity;
int _HectonWeatherStateMask;
float4 _HectonNoirFogStratification;
float4 _HectonSedimentWorldRect;
float4 _HectonSedimentOverlayParamsA;
float4 _HectonSedimentOverlayParamsB;
float4 _HectonSedimentTintA;
float4 _HectonSedimentTintB;
float4 _HectonParasiteAnchorData[HECTON_PARASITE_MAX_ANCHORS];
float4 _HectonParasiteAnchorParams[HECTON_PARASITE_MAX_ANCHORS];
float4 _HectonParasiteGlobals;
float4 _HectonSubmarineCrushCenterRadius;
float4 _HectonSubmarineCrushDepthParams;
float4 _HectonHullDents[HECTON_HULL_DENT_MAX]; // xyz=local impact point, w=packed radius/depth
float4 _HectonHullDentParams;                  // x=active count, y=scar proxy weight, z=scar scalar, w=quality weight byte
float4 _HectonHabitatStressCenterRadius; // xyz=center, w=radius
float4 _HectonHabitatStressParams;       // x=stress, y=max displacement, z=grid scale, w=seed
float4 _HectonXRFoveatedParams;        // x=active, y=periphery resolve weight, z=reserved, w=refresh Hz
float4 _HectonXRFoveatedCenterRadius;  // xy=stereo view-space tangent center, z=inner 30-degree proxy, w=outer periphery
float4 _HectonVRSomaticComfortState;   // x=FOV tunnel, y=horizon blend, z=foveated multiplier, w=max pressure
float4 _HectonXRNearClipDitherParams;  // x=active, y=fade start meters, z=fade kill meters, w=dither intensity
float4 _HectonXROriginShiftState;      // x=XR active, y=origin shift sequence, z=pose refresh marker, w=fixed alpha
float4 _TotalUniverseOffset;           // xyz=runtime-to-absolute offset used for AUP-stable visual phase
float _AupJitterMask;                  // 1 during the AUP shift render frame; rounds camera-relative vertices to millimeters
float _HectonMathLodDistanceSq;        // C# scalability bridge debug/readback value
float4 _HectonWorldShake;              // xyz=seismic vertex offset, w=intensity
float _HectonEquipmentRust01;          // global equipment corrosion scalar, 0 clean -> 1 ruined
float4 _HectonMaterialDecayRuntime;    // x=rust01, y=recent wetness01, z=quality pressure, w=stable seed
float4 _HectonPlayerBloodSplatter;     // x=stress01, y=health damage01, z=gloss boost, w=active01
float _InternalWaterlineY;
float4 _InternalWaterlineRuntime;      // xyz/w owned by InternalFloodWaterlineRuntime

// Math-LOD weight and the safe-normalize built on it now live in HectonMathLod.hlsl, together with
// the _HectonMathLodMode / _HectonMathLodWeight globals declared just above this block before the
// move. Passes that build geometry but do not want the whole of CoreLit (indirect particle and
// vegetation motion-vector passes) include that file directly, so they snap or stay exact in step
// with ForwardLit instead of carrying a private copy that cannot see the weight. Visibility for
// every existing CoreLit consumer is unchanged.
#include "Assets/_Project/Art/Shaders/HectonMathLod.hlsl"

float HectonCoreLitApproxDistance(float3 delta)
{
    float3 a = abs(delta);
    float maxAxis = max(max(a.x, a.y), a.z);
    float minAxis = min(min(a.x, a.y), a.z);
    float midAxis = a.x + a.y + a.z - maxAxis - minAxis;
    return maxAxis + midAxis * 0.375 + minAxis * 0.1875;
}

int HectonCoreLitRoundToIntFast(float value)
{
    return value >= 0.0 ? (int)(value + 0.5) : (int)(value - 0.5);
}

float3 HectonCoreLitSanitizePositionOS(float3 positionOS)
{
    return all(isfinite(positionOS)) ? positionOS : float3(0.0, 0.0, 0.0);
}

void HectonCoreLitUnpackHullDent(float packedRadiusDepth, out float radius, out float depth)
{
    float safePacked = isfinite(packedRadiusDepth) ? max(0.0, packedRadiusDepth) : 0.0;
    float depthQ = floor(safePacked * rcp(256.0));
    float radiusQ = safePacked - depthQ * 256.0;
    radius = max(radiusQ * 0.0625, 0.001);
    depth = saturate(depthQ * rcp(255.0));
}

float HectonCoreLitHullDentExactWeight()
{
    return saturate(1.0 - _HectonHullDentParams.y);
}

float HectonCoreLitEvaluateHullDentDepthOS(float3 positionOS)
{
    float3 safePositionOS = HectonCoreLitSanitizePositionOS(positionOS);
    float dentDepth = 0.0;
    float exactWeight = HectonCoreLitHullDentExactWeight();

    if (_HectonHullDentParams.x <= 0.5 || exactWeight <= 0.0001)
        return 0.0;

    [unroll]
    for (int i = 0; i < HECTON_HULL_DENT_MAX; i++)
    {
        float4 dent = _HectonHullDents[i];
        float radius;
        float depth;
        HectonCoreLitUnpackHullDent(dent.w, radius, depth);
        if (depth <= 0.0001)
            continue;

        float3 delta = safePositionOS - dent.xyz;
        float distSq = dot(delta, delta);
        float radiusSq = max(radius * radius, 0.000001);
        if (distSq >= radiusSq)
            continue;

        float falloff = saturate(1.0 - distSq * rcp(radiusSq));
        dentDepth = max(dentDepth, falloff * falloff * depth);
    }

    return dentDepth * exactWeight;
}

float3 HectonCoreLitApplyHullDentsOS(float3 positionOS, float3 normalOS, out half dentShadow)
{
    float3 safePositionOS = HectonCoreLitSanitizePositionOS(positionOS);
    if (_HectonHullDentParams.x <= 0.5)
    {
        dentShadow = 0.0h;
        return safePositionOS;
    }

    float dentDepth = HectonCoreLitEvaluateHullDentDepthOS(safePositionOS);
    dentShadow = (half)saturate(dentDepth * 4.0);

    if (dentDepth <= 0.0001)
        return safePositionOS;

    float3 safeNormalOS = HectonCoreLitSafeNormalize(normalOS);
    return HectonCoreLitSanitizePositionOS(safePositionOS - safeNormalOS * dentDepth);
}

void HectonCoreLitApplyHullDentSurfaceCheat(half dentShadow, inout half3 albedo, inout half smoothness)
{
    half shadow = saturate(dentShadow);
    if (shadow <= 0.0001h)
        return;

    albedo = lerp(albedo, albedo * half3(0.42h, 0.45h, 0.48h), shadow);
    smoothness = lerp(smoothness, smoothness * 0.58h, shadow);
}

float3 HectonCoreLitSanitizePositionWS(float3 positionWS)
{
    float3 sanitized = all(isfinite(positionWS)) ? positionWS : float3(0.0, 0.0, 0.0);
    float jitterMask = saturate(_AupJitterMask);
    if (jitterMask > 0.0001)
    {
        float3 cameraRelative = sanitized - _WorldSpaceCameraPos;
        cameraRelative = round(cameraRelative * 1000.0) * 0.001;
        sanitized = _WorldSpaceCameraPos + cameraRelative;
    }

    return sanitized;
}

float3 HectonCoreLitApplyWorldShake(float3 positionWS)
{
    float3 sanitized = HectonCoreLitSanitizePositionWS(positionWS);
    float intensity = saturate(_HectonWorldShake.w) * HectonCoreLitMathLodWeight();
    if (intensity <= 0.0001)
        return sanitized;

    return HectonCoreLitSanitizePositionWS(sanitized + _HectonWorldShake.xyz * intensity);
}

float HectonCoreLitInterleavedGradientNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

float HectonCoreLitLinearRamp(float edge0, float edge1, float value)
{
    float span = edge1 - edge0;
    float invSpan = rcp(max(abs(span), 0.0001));
    float direction = span >= 0.0 ? 1.0 : -1.0;
    return saturate((value - edge0) * invSpan * direction);
}

float2 HectonCoreLitResolveTaaDitherPhasePixel(float2 pixel)
{
    uint2 pixelParity = (uint2)pixel & 1u;
    uint phaseIndex = pixelParity.x | (pixelParity.y << 1u);
    return float2((float)(phaseIndex & 1u), (float)((phaseIndex >> 1u) & 1u)) * 0.5;
}

float HectonCoreLitTaaAccumulatedInterleavedGradientNoise(float2 pixel)
{
    return HectonCoreLitInterleavedGradientNoise(pixel + HectonCoreLitResolveTaaDitherPhasePixel(pixel));
}

half HectonCoreLitBlueNoise4x4(float2 pixel)
{
    uint2 pixel4 = (uint2)pixel & 3u;
    uint index = pixel4.x + pixel4.y * 4u;
    switch (index)
    {
        case 0u: return 0.90625h;
        case 1u: return 0.53125h;
        case 2u: return 0.71875h;
        case 3u: return 0.84375h;
        case 4u: return 0.03125h;
        case 5u: return 0.78125h;
        case 6u: return 0.15625h;
        case 7u: return 0.34375h;
        case 8u: return 0.40625h;
        case 9u: return 0.65625h;
        case 10u: return 0.59375h;
        case 11u: return 0.96875h;
        case 12u: return 0.09375h;
        case 13u: return 0.46875h;
        case 14u: return 0.28125h;
        default: return 0.21875h;
    }
}

float HectonCoreLitHash12(float2 value)
{
    float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
    hash += dot(hash, hash.yzx + 33.33);
    return frac((hash.x + hash.y) * hash.z);
}

float HectonCoreLitTemporalSinFlicker01(float timeSeconds, float speed, float phaseOffset)
{
    float phase = (timeSeconds * max(speed, 0.001) + phaseOffset) * 0.15915494 + 0.25;
    return 1.0 - abs(frac(phase) * 2.0 - 1.0);
}

float HectonCoreLitTrianglePulse01(float phase)
{
    return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
}

float HectonCoreLitTriangle01(float value)
{
    return 1.0 - abs(frac(value) * 2.0 - 1.0);
}

float HectonCoreLitFastPower01(float value, float exponent)
{
    float v = saturate(value);
    float v2 = v * v;
    float v4 = v2 * v2;
    float v8 = v4 * v4;
    float low = lerp(v, v2, saturate(exponent - 1.0));
    float high = lerp(v2, v8, saturate((exponent - 2.0) * 0.16666667));
    return lerp(low, high, step(2.0, exponent));
}

float HectonCoreLitHologramFlickerGate(float4 positionCS, float3 absolutePosition, float timeSeconds, float speed, float cutoff)
{
    float2 pixel = floor(positionCS.xy);
    float2 worldCell = floor(absolutePosition.xz * 7.0 + absolutePosition.y * 0.31);
    float spatialPhase = dot(pixel + worldCell, float2(12.9898, 78.233));
    float ignLike = HectonCoreLitHash12(pixel + worldCell);
    float temporal = HectonCoreLitTemporalSinFlicker01(timeSeconds, speed, spatialPhase);
    return min(ignLike, temporal) - cutoff;
}

float2 HectonCoreLitResolveFragmentScreenUV(float4 positionCS)
{
    float2 screenUV = positionCS.xy / max(_ScaledScreenParams.xy, float2(1.0, 1.0));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
#endif
    return screenUV;
}

float2 HectonCoreLitResolveClipScreenUV(float4 positionCS)
{
    float2 screenUV = positionCS.xy * rcp(positionCS.w) * 0.5 + 0.5;
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
#endif
    return saturate(screenUV);
}

float2 HectonCoreLitResolveFoveatedSourceUV(float2 linearScreenUV)
{
    return FoveatedRemapLinearToNonUniform(saturate(linearScreenUV));
}

float2 HectonCoreLitBuildStereoFoveationVector(float3 positionWS)
{
    float3 positionVS = TransformWorldToView(positionWS);
    return positionVS.xy * rcp(max(abs(positionVS.z), 0.0001));
}

float HectonCoreLitEvaluateXRFoveatedMask(float2 stereoFoveationVector)
{
    if (_HectonXRFoveatedParams.x <= 0.5)
        return 0.0;

    float innerRadius = max(_HectonXRFoveatedCenterRadius.z, 0.001);
    float outerRadius = max(_HectonXRFoveatedCenterRadius.w, innerRadius + 0.001);
    float2 radialDelta = stereoFoveationVector - _HectonXRFoveatedCenterRadius.xy;
    float radialDistanceSq = dot(radialDelta, radialDelta);
    float somaticScale = isfinite(_HectonVRSomaticComfortState.z) ? clamp(_HectonVRSomaticComfortState.z, 1.0, 2.75) : 1.0;
    float somaticPressure = isfinite(_HectonVRSomaticComfortState.w) ? saturate(_HectonVRSomaticComfortState.w) : 0.0;
    float resolveWeight = saturate(_HectonXRFoveatedParams.y * lerp(1.0, somaticScale, somaticPressure));
    return HectonCoreLitLinearRamp(innerRadius * innerRadius, outerRadius * outerRadius, radialDistanceSq) * resolveWeight;
}

bool HectonCoreLitShouldRunXRFullQuality(float2 stereoFoveationVector)
{
    return HectonCoreLitEvaluateXRFoveatedMask(stereoFoveationVector) < 0.5;
}

half3 HectonCoreLitApplyXRFoveatedResolve(half3 color, float2 stereoFoveationVector)
{
    half mask = (half)HectonCoreLitEvaluateXRFoveatedMask(stereoFoveationVector);
    if (mask <= 0.0001h)
        return color;

    half3 quantized = floor(color * 48.0h + 0.5h) * (1.0h / 48.0h);
    return lerp(color, quantized, mask);
}

float HectonCoreLitEvaluateXRNearClipFade(float3 positionWS)
{
    if (_HectonXRNearClipDitherParams.x <= 0.5)
        return 1.0;

    float fadeStartMeters = max(_HectonXRNearClipDitherParams.y, 0.001);
    float fadeKillMeters = max(_HectonXRNearClipDitherParams.z, 0.0001);
    float3 vertexAup = positionWS + _TotalUniverseOffset.xyz;
    float3 cameraAup = _WorldSpaceCameraPos + _TotalUniverseOffset.xyz;
    float3 eyeDelta = vertexAup - cameraAup;
    float distanceSqToEye = dot(eyeDelta, eyeDelta);
    return HectonCoreLitLinearRamp(fadeKillMeters * fadeKillMeters, fadeStartMeters * fadeStartMeters, distanceSqToEye);
}

void HectonCoreLitClipXRNearWallDither(float nearClipFade, float4 positionCS)
{
    if (_HectonXRNearClipDitherParams.x <= 0.5)
        return;

    float keepCoverage = saturate(nearClipFade);
    float noise = HectonCoreLitInterleavedGradientNoise(floor(positionCS.xy) + _HectonXROriginShiftState.yz);
    clip(keepCoverage - noise * saturate(_HectonXRNearClipDitherParams.w));
}

void HectonCoreLitClipXRNearWallDither(float3 positionWS, float4 positionCS)
{
    HectonCoreLitClipXRNearWallDither(HectonCoreLitEvaluateXRNearClipFade(positionWS), positionCS);
}

half HectonCoreLitResolveVertexAmbientOcclusion(float bakedAo)
{
    return (half)saturate(bakedAo);
}

float4 HectonCoreLitApplyClipSpaceDepthBias(float4 positionCS, float depthBias, float depthBiasMask)
{
    float maskedBias = max(depthBias, 0.0) * saturate(depthBiasMask);
    if (maskedBias <= 0.0)
        return positionCS;

    float clipBias = maskedBias * max(positionCS.w, 0.0001);
#if UNITY_REVERSED_Z
    positionCS.z += clipBias;
#else
    positionCS.z -= clipBias;
#endif
    return positionCS;
}

float2 HectonCoreLitHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float HectonCoreLitSampleTilingVoronoi(float2 p)
{
    float2 baseCell = floor(p);
    float2 localPosition = frac(p);
    float nearest = 8.0;
    float secondNearest = 8.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2((float)x, (float)y);
            float2 feature = HectonCoreLitHash22(baseCell + neighbor);
            float2 delta = neighbor + feature - localPosition;
            float distanceSq = dot(delta, delta);
            if (distanceSq < nearest)
            {
                secondNearest = nearest;
                nearest = distanceSq;
            }
            else if (distanceSq < secondNearest)
            {
                secondNearest = distanceSq;
            }
        }
    }

    float nearest01 = saturate(nearest);
    float cellCore = 1.0 - nearest01;
    float cellRidge = 1.0 - saturate((secondNearest - nearest) * 1.35);
    return saturate(cellCore * 0.42 + cellRidge * 0.72);
}

float HectonCoreLitSampleSubmarineCrushBuckling(float3 positionWS)
{
    float scale = max(_HectonSubmarineCrushDepthParams.w, 0.001);
    float3 centeredPosition = (positionWS - _HectonSubmarineCrushCenterRadius.xyz) * scale;
    float cellBreakup = HectonCoreLitSampleTilingVoronoi(centeredPosition.xz + centeredPosition.y * 0.17);
    float crossBreakup = HectonCoreLitSampleTilingVoronoi(centeredPosition.xy * 1.73 + centeredPosition.z * 0.11 + 11.37);
    float crease = 1.0 - abs(frac(dot(centeredPosition, float3(0.31, 0.47, 0.19))) * 2.0 - 1.0);
    return saturate(max(cellBreakup, crossBreakup * 0.82) + crease * 0.32);
}

float3 HectonCoreLitApplyHabitatAnalyticalStress(float3 positionWS, float3 normalWS)
{
    float sourceStress01 = _HectonHabitatStressParams.x;
    float displacementSource = _HectonHabitatStressParams.y;
    if (!isfinite(sourceStress01) || !isfinite(displacementSource))
        return positionWS;

    float stress01 = saturate(sourceStress01);
    float displacementMax = max(displacementSource, 0.0);
    if (stress01 <= HECTON_CORE_LIT_HABITAT_STRESS_EPSILON ||
        displacementMax <= HECTON_CORE_LIT_HABITAT_DISPLACEMENT_EPSILON)
        return positionWS;

    float radiusSource = _HectonHabitatStressCenterRadius.w;
    if (!isfinite(radiusSource))
        return positionWS;

    float radius = max(radiusSource, 0.0);
    float3 radiusDelta = positionWS - _HectonHabitatStressCenterRadius.xyz;
    float radiusMask = radius > 0.001
        ? 1.0 - saturate(dot(radiusDelta, radiusDelta) * rcp(max(radius * radius, 0.0001)))
        : 1.0;
    if (!isfinite(radiusMask) || radiusMask <= 0.0001)
        return positionWS;

    float gridScaleSource = _HectonHabitatStressParams.z;
    float seedSource = _HectonHabitatStressParams.w;
    if (!isfinite(gridScaleSource) || !isfinite(seedSource))
        return positionWS;

    float gridScale = max(gridScaleSource, 0.001);
    float seed = seedSource * 0.0137;
    float3 q = floor((positionWS + _TotalUniverseOffset.xyz) * gridScale);
    float phaseA = dot(q, float3(0.31, 0.47, 0.19)) + seed;
    float phaseB = dot(q.yzx + 17.0, float3(0.23, 0.11, 0.41)) - seed;
    float triA = HectonCoreLitTrianglePulse01(phaseA) * 2.0 - 1.0;
    float triB = HectonCoreLitTrianglePulse01(phaseB) * 2.0 - 1.0;
    float dent = (triA * 0.68 + triB * 0.32) * displacementMax * stress01 * radiusMask;
    return HectonCoreLitSanitizePositionWS(positionWS + normalWS * dent);
}

float3 HectonCoreLitApplySubmarineCrushDepth(float3 positionWS, float3 normalWS)
{
    positionWS = HectonCoreLitSanitizePositionWS(positionWS);
    normalWS = HectonCoreLitSafeNormalize(normalWS);
    float currentDepth = max(_HectonSubmarineCrushDepthParams.x, 0.0);
    float crushDepth = max(_HectonSubmarineCrushDepthParams.y, 0.001);
    float depth01 = saturate(currentDepth * rcp(crushDepth));
    float displacementMax = max(_HectonSubmarineCrushDepthParams.z, 0.0);
    if (depth01 <= 0.0001 || displacementMax <= 0.0001)
        return HectonCoreLitApplyWorldShake(HectonCoreLitApplyHabitatAnalyticalStress(positionWS, normalWS));

    float radius = max(_HectonSubmarineCrushCenterRadius.w, 0.0);
    float3 radiusDelta = positionWS - _HectonSubmarineCrushCenterRadius.xyz;
    float radiusMask = radius > 0.001
        ? 1.0 - saturate(dot(radiusDelta, radiusDelta) * rcp(max(radius * radius, 0.0001)))
        : 1.0;
    if (radiusMask <= 0.0001)
        return HectonCoreLitApplyWorldShake(HectonCoreLitApplyHabitatAnalyticalStress(positionWS, normalWS));

    float buckling = HectonCoreLitSampleSubmarineCrushBuckling(positionWS);
    float ridge = buckling * buckling;
    float buckle = (buckling * 2.0 - 1.0) * 0.68 - ridge * 0.32;
    float displacement = buckle * displacementMax * depth01 * radiusMask;
    return HectonCoreLitApplyWorldShake(HectonCoreLitApplyHabitatAnalyticalStress(HectonCoreLitSanitizePositionWS(positionWS + normalWS * displacement), normalWS));
}

float HectonCoreLitSedimentRippleHeight(float2 uv)
{
    float layerA = HectonCoreLitTrianglePulse01(uv.x * 1.73 + uv.y * 0.47) * 2.0 - 1.0;
    float layerB = HectonCoreLitTrianglePulse01(uv.y * 1.91 - uv.x * 0.29 + 1.5707963) * 2.0 - 1.0;
    float layerC = HectonCoreLitTrianglePulse01((uv.x + uv.y) * 0.63 + 1.7) * 2.0 - 1.0;
    return layerA * 0.5 + layerB * 0.35 + layerC * 0.15;
}

float HectonCoreLitSampleSedimentMaskFromUnitNormal(float3 normalizedNormalWS)
{
    if (_HectonSedimentOverlayParamsA.x <= 0.5)
        return 0.0;

    float topDownMask = saturate(normalizedNormalWS.y);
    float upFacing = saturate((normalizedNormalWS.y - _HectonSedimentOverlayParamsA.y) * _HectonSedimentOverlayParamsA.z);
    return saturate(topDownMask * upFacing * _HectonSedimentOverlayParamsB.w);
}

float HectonCoreLitSampleSedimentMask(float3 normalWS)
{
    return HectonCoreLitSampleSedimentMaskFromUnitNormal(HectonCoreLitSafeNormalize(normalWS));
}

void HectonCoreLitApplySedimentOverlay(
    float3 positionWS,
    inout half3 normalWS,
    inout half3 albedo,
    inout half metallic,
    inout half smoothness)
{
    float3 baseNormal = HectonCoreLitSafeNormalize(normalWS);
    float strength = HectonCoreLitSampleSedimentMaskFromUnitNormal(baseNormal);
    if (strength <= 0.0001)
        return;

    float2 rippleUv = positionWS.xz * _HectonSedimentOverlayParamsA.w;
    float baseHeight = HectonCoreLitSedimentRippleHeight(rippleUv);
    float heightDx = HectonCoreLitSedimentRippleHeight(rippleUv + float2(0.09, 0.0));
    float heightDy = HectonCoreLitSedimentRippleHeight(rippleUv + float2(0.0, 0.09));
    float2 gradient = float2(heightDx - baseHeight, heightDy - baseHeight) * _HectonSedimentOverlayParamsB.x;

    float3 tangentWS = abs(baseNormal.y) < 0.999 ? HectonCoreLitSafeNormalize(cross(float3(0.0, 1.0, 0.0), baseNormal)) : float3(1.0, 0.0, 0.0);
    float3 bitangentWS = HectonCoreLitSafeNormalize(cross(baseNormal, tangentWS));
    float3 sedimentNormal = HectonCoreLitSafeNormalize(baseNormal - tangentWS * gradient.x - bitangentWS * gradient.y);
    sedimentNormal = HectonCoreLitSafeNormalize(lerp(sedimentNormal, float3(0.0, 1.0, 0.0), strength * 0.25));

    float rippleBlend = saturate(baseHeight * 0.5 + 0.5);
    half3 sedimentColor = (half3)lerp(_HectonSedimentTintA.rgb, _HectonSedimentTintB.rgb, rippleBlend);
    albedo = lerp(albedo, sedimentColor, (half)strength);
    normalWS = (half3)HectonCoreLitSafeNormalize(lerp(baseNormal, sedimentNormal, strength));
    metallic = lerp(metallic, (half)_HectonSedimentOverlayParamsB.y, (half)strength);
    smoothness = lerp(smoothness, (half)_HectonSedimentOverlayParamsB.z, (half)strength);
}

float HectonCoreLitValueNoise2(float2 value)
{
    float2 cell = floor(value);
    float2 fracValue = frac(value);
    float2 smoothValue = fracValue * fracValue * (3.0 - 2.0 * fracValue);

    float a = HectonCoreLitHash22(cell).x;
    float b = HectonCoreLitHash22(cell + float2(1.0, 0.0)).x;
    float c = HectonCoreLitHash22(cell + float2(0.0, 1.0)).x;
    float d = HectonCoreLitHash22(cell + float2(1.0, 1.0)).x;
    return lerp(lerp(a, b, smoothValue.x), lerp(c, d, smoothValue.x), smoothValue.y);
}

struct HectonPackedMaskV1
{
    half metallic;
    half occlusion;
    half smoothness;
    half emissionMask;
};

HectonPackedMaskV1 HectonCoreLitDecodePackedMaskV1(
    half4 packedMask,
    half metallicScale,
    half occlusionStrength,
    half smoothnessScale)
{
    HectonPackedMaskV1 decoded;
    decoded.metallic = saturate(packedMask.r * metallicScale);
    decoded.occlusion = saturate(lerp(1.0h, packedMask.g, occlusionStrength));
    decoded.smoothness = saturate(packedMask.b * smoothnessScale);
    decoded.emissionMask = saturate(packedMask.a);
    return decoded;
}

HectonPackedMaskV1 HectonCoreLitDecodeStrictPropMask(
    half4 packedMask,
    half metallicScale,
    half occlusionStrength,
    half smoothnessScale)
{
    return HectonCoreLitDecodePackedMaskV1(packedMask, metallicScale, occlusionStrength, smoothnessScale);
}

half HectonCoreLitResolveDitheredFadeNoise(float4 positionCS)
{
    float safeW = max(abs(positionCS.w), 0.0001);
    float2 screenUV = positionCS.xy * rcp(safeW) * 0.5 + 0.5;
    float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
#if defined(HECTON_USE_4X4_BLUE_NOISE_FALLBACK)
    return HectonCoreLitBlueNoise4x4(pixel);
#else
    return (half)HectonCoreLitTaaAccumulatedInterleavedGradientNoise(pixel);
#endif
}

void HectonCoreLitClipDitheredTransparencyFade(half fadeAmount, float4 positionCS)
{
    half noiseValue = HectonCoreLitResolveDitheredFadeNoise(positionCS);
    clip(noiseValue - saturate(fadeAmount));
}

half HectonCoreLitResolveMx350ShadowDither(half shadowAttenuation, float4 positionCS)
{
#if defined(_SHADOWS_SOFT) || defined(_SHADOWS_SOFT_LOW) || defined(_SHADOWS_SOFT_MEDIUM) || defined(_SHADOWS_SOFT_HIGH)
    return shadowAttenuation;
#else
    half noiseValue = HectonCoreLitResolveDitheredFadeNoise(positionCS);
    half penumbraMask = 1.0h - saturate(abs(shadowAttenuation - 0.5h) * 2.0h);
    half ditheredShadow = (half)step(noiseValue, shadowAttenuation);
    return saturate(lerp(shadowAttenuation, ditheredShadow, penumbraMask * 0.55h));
#endif
}

half HectonCoreLitResolveSceneDepthCutoutFade(float4 positionCS, float fadeDistanceMeters)
{
    if (!all(isfinite(positionCS)))
        return 1.0h;

    float2 screenUV = HectonCoreLitResolveFragmentScreenUV(positionCS);
    float sceneRawDepth = SampleSceneDepth(HectonCoreLitResolveFoveatedSourceUV(screenUV));
#if UNITY_REVERSED_Z
    float sceneDepthValid = step(0.0001, sceneRawDepth);
#else
    float sceneDepthValid = step(sceneRawDepth, 0.9999);
#endif
    if (sceneDepthValid <= 0.5)
        return 1.0h;

    float rawFragmentDepth = saturate(positionCS.z);
    float sceneDepthMeters = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
    float fragmentDepthMeters = LinearEyeDepth(rawFragmentDepth, _ZBufferParams);
    float distanceMeters = max(fadeDistanceMeters, 0.001);
    return (half)saturate((sceneDepthMeters - fragmentDepthMeters) * rcp(distanceMeters));
}

half HectonCoreLitResolveFlatNoirLod(float3 positionWS)
{
    float3 cameraDelta = positionWS - _WorldSpaceCameraPos;
    return (half)step(400.0, dot(cameraDelta, cameraDelta));
}

half HectonCoreLitEvaluateWrapDiffuse(float3 normalWS, float3 lightDirWS)
{
    const half wrap = 0.5h;
    return (half)(max(0.0, dot(normalWS, lightDirWS) + wrap) / (1.0h + wrap));
}

half3 HectonCoreLitEvaluateCheapBacklightSss(
    float3 normalWS,
    float3 lightDirWS,
    half3 fleshColor,
    half strength)
{
    half wrapDiffuse = HectonCoreLitEvaluateWrapDiffuse(normalWS, lightDirWS);
    half backLight = saturate(1.0h - wrapDiffuse);
    return fleshColor * backLight * saturate(strength);
}

half4 HectonCoreLitSampleStochastic2D(TEXTURE2D_PARAM(sourceTexture, sourceSampler), float2 uv, float2 seed, half strength)
{
    half blendStrength = saturate(strength);
    if (blendStrength <= 0.0001h)
        return SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uv);

    float2 tile = floor(uv);
    float2 jitterA = HectonCoreLitHash22(tile + floor(seed)) - 0.5;
    float2 jitterB = HectonCoreLitHash22(tile + floor(seed * 0.37) + 19.17) - 0.5;
    half selector = (half)HectonCoreLitInterleavedGradientNoise(tile + seed);
    half stochasticBlend = (half)HectonCoreLitLinearRamp(0.24, 0.76, selector) * blendStrength;
    float2 uvA = uv + jitterA;
    float2 uvB = uv * 1.013 + jitterB;
    [branch]
    if (stochasticBlend <= 0.0001h)
        return SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvA);

    [branch]
    if (stochasticBlend >= 0.999h)
        return SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvB);

    half4 sampleA = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvA);
    half4 sampleB = SAMPLE_TEXTURE2D(sourceTexture, sourceSampler, uvB);
    return lerp(sampleA, sampleB, stochasticBlend);
}

void HectonCoreLitBuildSurfaceFrameFromUnitNormal(float3 normalizedNormal, out float3 tangentWS, out float3 bitangentWS)
{
    tangentWS = abs(normalizedNormal.y) < 0.999
        ? HectonCoreLitSafeNormalize(cross(float3(0.0, 1.0, 0.0), normalizedNormal))
        : float3(1.0, 0.0, 0.0);
    bitangentWS = HectonCoreLitSafeNormalize(cross(normalizedNormal, tangentWS));
}

void HectonCoreLitBuildSurfaceFrame(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
{
    HectonCoreLitBuildSurfaceFrameFromUnitNormal(HectonCoreLitSafeNormalize(normalWS), tangentWS, bitangentWS);
}

half3 HectonCoreLitApplyTripleDetailMicroNormals(
    float3 positionWS,
    half3 normalWS,
    half strength,
    half tiling,
    half nearDistanceMeters)
{
    half resolvedStrength = saturate(strength);
    if (resolvedStrength <= 0.0001h)
        return normalWS;

    float nearDistance = max((float)nearDistanceMeters, 0.25);
    float fadeStart = nearDistance * 0.82;
    float3 eyeDelta = positionWS - _WorldSpaceCameraPos;
    float distanceSq = dot(eyeDelta, eyeDelta);
    float nearDistanceSq = nearDistance * nearDistance;
    if (distanceSq >= nearDistanceSq)
        return normalWS;

    float fadeStartSq = fadeStart * fadeStart;
    half nearMask = (half)(1.0 - HectonCoreLitLinearRamp(fadeStartSq, nearDistanceSq, distanceSq));
    if (nearMask <= 0.0001h)
        return normalWS;

    float3 baseNormal = HectonCoreLitSafeNormalize(normalWS);
    float3 tangentWS;
    float3 bitangentWS;
    HectonCoreLitBuildSurfaceFrameFromUnitNormal(baseNormal, tangentWS, bitangentWS);

    float2 surfaceUv = float2(dot(positionWS, tangentWS), dot(positionWS, bitangentWS)) * max((float)tiling, 0.01);
    half3 microA = UnpackNormalScale(SAMPLE_TEXTURE2D(_HectonMicroNormalTex, sampler_HectonMicroNormalTex, surfaceUv), 1.0h);
    half3 microB = UnpackNormalScale(SAMPLE_TEXTURE2D(_HectonMicroNormalTex, sampler_HectonMicroNormalTex, surfaceUv * 2.03 + float2(17.13, 3.71)), 1.0h);
    half3 microC = UnpackNormalScale(SAMPLE_TEXTURE2D(_HectonMicroNormalTex, sampler_HectonMicroNormalTex, surfaceUv * 4.17 + float2(-9.41, 21.37)), 1.0h);
    half2 microXY = microA.xy * 0.55h + microB.xy * 0.30h + microC.xy * 0.15h;

    float3 microNormalWS = HectonCoreLitSafeNormalize(
        baseNormal +
        tangentWS * (microXY.x * resolvedStrength) +
        bitangentWS * (microXY.y * resolvedStrength));
    return (half3)HectonCoreLitSafeNormalize(lerp(baseNormal, microNormalWS, resolvedStrength * nearMask));
}

void HectonCoreLitBuildTangentFrame(
    float3 normalWS,
    float3 tangentWS,
    float tangentSign,
    out float3 safeNormalWS,
    out float3 safeTangentWS,
    out float3 safeBitangentWS)
{
    safeNormalWS = HectonCoreLitSafeNormalize(normalWS);
    safeTangentWS = HectonCoreLitSafeNormalize(tangentWS);
    float handedness = tangentSign < 0.0 ? -1.0 : 1.0;
    safeBitangentWS = HectonCoreLitSafeNormalize(cross(safeNormalWS, safeTangentWS) * handedness);
}

half HectonCoreLitResolveDynamicRust01()
{
    return (half)saturate(max(_HectonEquipmentRust01, _HectonMaterialDecayRuntime.x));
}

float2 HectonCoreLitResolveDynamicWearUv(
    float2 baseUv,
    float3 viewDirWS,
    float3 normalWS,
    float3 tangentWS,
    float tangentSign,
    out half4 rustPacked,
    out half rustMask)
{
    half rust01 = HectonCoreLitResolveDynamicRust01();
    float2 rustUv = baseUv * _RustDetailMap_ST.xy + _RustDetailMap_ST.zw;
    rustMask = rust01;

    if (rust01 <= 0.0001h)
    {
        rustPacked = half4(0.0h, 0.5h, 0.5h, 1.0h);
        rustMask = 0.0h;
        return baseUv;
    }

    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, rustUv);

    float qualityPressure = saturate(_HectonMaterialDecayRuntime.z);
    float rustPomWeight = (1.0 - smoothstep(0.18, 0.72, qualityPressure)) * HectonCoreLitMathLodWeight();
    if (rust01 <= 0.3001h || rustPomWeight <= 0.001)
        return baseUv;

    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    HectonCoreLitBuildTangentFrame(normalWS, tangentWS, tangentSign, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 safeViewWS = HectonCoreLitSafeNormalize(viewDirWS);
    float3 viewDirTS = float3(dot(safeViewWS, safeTangentWS), dot(safeViewWS, safeBitangentWS), max(dot(safeViewWS, safeNormalWS), 0.24));
    float viewInvZ = rcp(max(abs(viewDirTS.z), 0.24));
    float2 parallaxStep = viewDirTS.xy * viewInvZ * (0.012 + rust01 * 0.026) * rust01 * rustPomWeight;
    float2 resolvedUv = rustUv;
    float layerDepth = 0.0;

    [unroll]
    for (int stepIndex = 0; stepIndex < 4; stepIndex++)
    {
        half sampledHeight = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, resolvedUv).r;
        half stepMask = (half)step(layerDepth, sampledHeight);
        resolvedUv -= parallaxStep * (0.25 * stepMask);
        layerDepth += 0.25;
    }

    rustPacked = SAMPLE_TEXTURE2D(_RustDetailMap, sampler_RustDetailMap, resolvedUv);
    half pitMask = saturate((rustPacked.r - 0.34h) * 1.85h);
    rustMask = saturate(rust01 * (0.58h + pitMask * 0.42h));
    half2 pitNormal = (rustPacked.gb * 2.0h - 1.0h) * (rustMask * 0.0035h);
    return resolvedUv - _RustDetailMap_ST.zw + pitNormal;
}

half3 HectonCoreLitDecodeRustNormalTS(half4 rustPacked, half strength)
{
    half2 xy = (rustPacked.gb * 2.0h - 1.0h) * saturate(strength);
    half z = saturate(1.0h - dot(xy, xy) * 0.5h);
    return half3(xy, z);
}

void HectonCoreLitApplyDynamicWearPOM(
    float2 wearUv,
    float3 positionWS,
    float3 viewDirWS,
    float3 tangentWS,
    float tangentSign,
    half4 rustPacked,
    half rustMask,
    inout half3 albedo,
    inout half3 normalWS,
    inout half metallic,
    inout half smoothness)
{
    half rust01 = HectonCoreLitResolveDynamicRust01();
    half wearMask = saturate(max(rust01, rustMask));
    float3 safeNormalWS;
    float3 safeTangentWS;
    float3 safeBitangentWS;
    HectonCoreLitBuildTangentFrame(normalWS, tangentWS, tangentSign, safeNormalWS, safeTangentWS, safeBitangentWS);
    float3 safeViewWS = HectonCoreLitSafeNormalize(viewDirWS);
    half edgeWear = (half)HectonCoreLitFastPower01(1.0 - saturate(dot(safeNormalWS, safeViewWS)), 2.0);
    half finalRustMask = saturate(wearMask * (0.72h + edgeWear * 0.42h));

    if (finalRustMask > 0.0001h)
    {
        half3 rustNormalTS = HectonCoreLitDecodeRustNormalTS(rustPacked, finalRustMask * 0.82h);
        float3 rustNormalWS = HectonCoreLitSafeNormalize(
            safeTangentWS * rustNormalTS.x +
            safeBitangentWS * rustNormalTS.y +
            safeNormalWS * rustNormalTS.z);
        normalWS = (half3)HectonCoreLitSafeNormalize(lerp(safeNormalWS, rustNormalWS, finalRustMask));

        half pitRoughness = saturate(rustPacked.a);
        half heightCavity = saturate((rustPacked.r - 0.42h) * 1.72h);
        half3 rustTint = half3(0.55h, 0.19h, 0.055h);
        half3 pitTint = half3(0.16h, 0.055h, 0.028h);
        albedo = lerp(albedo, rustTint, finalRustMask * 0.62h);
        albedo = lerp(albedo, pitTint, heightCavity * finalRustMask * 0.35h);
        metallic = lerp(metallic, 0.0h, finalRustMask);
        smoothness = lerp(smoothness, saturate(1.0h - pitRoughness), finalRustMask);
    }

    half submerged = (half)step(positionWS.y, _InternalWaterlineY);
    half recentWet = saturate((half)max(_HectonMaterialDecayRuntime.y, _InternalWaterlineRuntime.z));
    half wetness = saturate(max(submerged, recentWet));
    if (wetness > 0.0001h)
    {
        albedo *= lerp(1.0h, 0.76h, wetness * 0.28h);
        smoothness = lerp(smoothness, 1.0h, wetness);
    }

    half bloodActive = saturate((half)_HectonPlayerBloodSplatter.w);
    if (bloodActive > 0.0001h)
    {
        half bloodSource = saturate((half)max(_HectonPlayerBloodSplatter.x, _HectonPlayerBloodSplatter.y));
        half noiseA = (half)HectonCoreLitHash12(floor(wearUv * 39.0 + _HectonMaterialDecayRuntime.w * 0.11));
        half noiseB = (half)HectonCoreLitHash12(floor(wearUv * 113.0 + 17.0));
        half patch = saturate((noiseA * 0.72h + noiseB * 0.28h - 0.56h) * 2.65h) * bloodSource * bloodActive;
        half3 bloodTint = half3(0.11h, 0.012h, 0.010h);
        albedo = lerp(albedo, bloodTint, patch * 0.72h);
        smoothness = lerp(smoothness, 1.0h, patch * saturate((half)_HectonPlayerBloodSplatter.z));
    }
}

half HectonCoreLitEvaluateTriplanarWearMask(float3 positionWS, half3 normalWS, half environmentalWear, half noiseScale)
{
    half wear = saturate(environmentalWear);
    if (wear <= 0.0001h)
        return 0.0h;

    float scale = max((float)noiseScale, 0.001);
    float3 normalAbs = abs(HectonCoreLitSafeNormalize(normalWS));
    normalAbs *= normalAbs;
    normalAbs *= normalAbs;
    normalAbs /= max(dot(normalAbs, float3(1.0, 1.0, 1.0)), 0.0001);
    half noiseX = (half)HectonCoreLitValueNoise2(positionWS.zy * scale + 11.37);
    half noiseY = (half)HectonCoreLitValueNoise2(positionWS.xz * scale + 23.71);
    half noiseZ = (half)HectonCoreLitValueNoise2(positionWS.xy * scale + 41.19);
    half triplanarNoise = noiseX * (half)normalAbs.x + noiseY * (half)normalAbs.y + noiseZ * (half)normalAbs.z;
    half cavityBias = saturate(1.0h - abs(normalWS.y) * 0.35h);
    return saturate((half)HectonCoreLitLinearRamp(0.34, 0.88, triplanarNoise) * wear * cavityBias);
}

void HectonCoreLitApplyEnvironmentalWear(
    float3 positionWS,
    half3 normalWS,
    half environmentalWear,
    half3 rustSaltColor,
    inout half3 albedo,
    inout half metallic,
    inout half smoothness)
{
    half wearMask = HectonCoreLitEvaluateTriplanarWearMask(positionWS, normalWS, environmentalWear, 0.145h);
    if (wearMask <= 0.0001h)
        return;

    half upwardSalt = saturate(normalWS.y * 0.5h + 0.5h);
    half blendStrength = saturate(wearMask * lerp(0.48h, 0.78h, upwardSalt));
    albedo = lerp(albedo, rustSaltColor, blendStrength);
    metallic = lerp(metallic, 0.0h, wearMask);
    smoothness = lerp(smoothness, smoothness * lerp(0.34h, 0.62h, upwardSalt), wearMask);
}

float HectonCoreLitResolveStormMask()
{
    const int WeatherStateStorm = 1 << 1;
    return (_HectonWeatherStateMask & WeatherStateStorm) != 0 ? saturate(_HectonWeatherIntensity) : 0.0;
}

float3 HectonCoreLitApplyStormRainDripVertexRipple(
    float3 positionWS,
    float3 normalWS,
    half amplitude,
    half tiling,
    half speed)
{
    float stormMask = HectonCoreLitResolveStormMask();
    if (stormMask <= 0.0001 || amplitude <= 0.0001h)
        return positionWS;

    float3 normal = HectonCoreLitSafeNormalize(normalWS);
    half verticalBase = saturate(1.0h - abs((half)normal.y));
    half verticalMask = verticalBase * verticalBase;
    if (verticalMask <= 0.0001h)
        return positionWS;

    float wallTiling = max((float)tiling, 0.01);
    float2 wallUv = float2(dot(positionWS.xz, float2(0.73, 0.41)), positionWS.y) * wallTiling;
    wallUv += float2(0.017, -0.083) * (HectonCoreLitResolveWrappedVisualTime() * max((float)speed, 0.01));
    half3 rippleNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D_LOD(_HectonMicroNormalTex, sampler_HectonMicroNormalTex, wallUv, 0), 1.0h);
    half ripple = (rippleNormalTS.x * 0.62h + rippleNormalTS.y * 0.38h) * verticalMask * (half)stormMask;
    return HectonCoreLitSanitizePositionWS(positionWS + normal * (ripple * amplitude));
}

void HectonCoreLitApplyProceduralRustSilt(
    float3 positionWS,
    half3 normalWS,
    half3 normalDetailWS,
    half edgeWearMask,
    half dirtAge01,
    half siltStrength,
    half rustStrength,
    half3 siltTint,
    half3 rustTint,
    inout half3 albedo,
    inout half metallic,
    inout half smoothness)
{
    half age = saturate(dirtAge01);
    half totalStrength = saturate(max(siltStrength, rustStrength) * age);
    if (totalStrength <= 0.0001h)
        return;

    float2 siltUv = positionWS.xz * 0.085;
    float2 rustUv = positionWS.xz * 0.173 + positionWS.y * 0.037;
    half broadNoise = (half)HectonCoreLitValueNoise2(siltUv);
    half fineNoise = (half)HectonCoreLitValueNoise2(rustUv * 2.7 + 13.7);
    half normalMicroCavity = saturate(1.0h - abs(normalDetailWS.y));
    half topDownBase = saturate(normalWS.y * 0.92h + 0.08h);
    half topDown = topDownBase * topDownBase * (1.22h - topDownBase * 0.22h);
    half siltMask = saturate(topDown * siltStrength * age * lerp(0.42h, 1.0h, broadNoise));
    half edgeRust = saturate(edgeWearMask * rustStrength * age);
    half rustBreakup = saturate((fineNoise - 0.38h) * 2.35h);
    half rustMask = saturate(edgeRust * rustBreakup * (0.55h + normalMicroCavity * 0.45h));

    albedo = lerp(albedo, siltTint, siltMask * 0.58h);
    albedo = lerp(albedo, rustTint, rustMask * 0.72h);
    metallic = lerp(metallic, 0.0h, saturate(siltMask + rustMask));
    smoothness = lerp(smoothness, smoothness * 0.45h, saturate(siltMask * 0.6h + rustMask));
}

float HectonCoreLitEvaluateParasiteField(float3 positionWS, out float pulseMultiplier, out float thermalGrowthMask)
{
    pulseMultiplier = 1.0;
    thermalGrowthMask = 0.0;

    int anchorCount = (int)min(_HectonParasiteGlobals.x, (float)HECTON_PARASITE_MAX_ANCHORS);
    if (anchorCount <= 0)
        return 0.0;

    float timeValue = _HectonParasiteGlobals.y;
    float pulseAmplitude = max(_HectonParasiteGlobals.z, 0.0);
    float feather = max(_HectonParasiteGlobals.w, 0.25);
    float bestMask = 0.0;

    [loop]
    for (int anchorIndex = 0; anchorIndex < HECTON_PARASITE_MAX_ANCHORS; anchorIndex++)
    {
        if (anchorIndex >= anchorCount)
            break;

        float4 anchor = _HectonParasiteAnchorData[anchorIndex];
        float4 parameters = _HectonParasiteAnchorParams[anchorIndex];
        float radius = max(anchor.w, 0.001);
        float3 anchorDelta = positionWS - anchor.xyz;
        float radiusSq = radius * radius;
        if (dot(anchorDelta, anchorDelta) >= radiusSq)
            continue;

        float distanceToAnchor = HectonCoreLitApproxDistance(anchorDelta);
        float normalizedDistance = 1.0 - saturate(distanceToAnchor / radius);
        float spread = HectonCoreLitFastPower01(normalizedDistance, feather);
        float candidateMask = spread * saturate(parameters.x);
        if (candidateMask <= bestMask)
            continue;

        bestMask = candidateMask;
        float pulsePhase = timeValue * max(parameters.y, 0.05) * 6.2831853 + distanceToAnchor * 0.35;
        pulseMultiplier = 1.0 + (HectonCoreLitTrianglePulse01(pulsePhase) * 2.0 - 1.0) * pulseAmplitude;
        thermalGrowthMask = saturate(parameters.z);
    }

    return saturate(bestMask);
}

float HectonCoreLitCheapCausticRidge(float2 uv, float cellDensity, float timePhase)
{
    float2 animatedUv = uv * cellDensity + float2(timePhase, -timePhase * 0.73);
    float stripeA = HectonCoreLitTriangle01(animatedUv.x + animatedUv.y * 0.37);
    float stripeB = HectonCoreLitTriangle01(animatedUv.x * -0.61 + animatedUv.y + 0.23);
    float cellMask = lerp(0.72, 1.0, HectonCoreLitHash12(floor(animatedUv)));
    return saturate((1.0 - abs(stripeA - stripeB)) * cellMask);
}

float HectonCoreLitEvaluateProceduralCaustics(float2 uv)
{
    return 0.0;
}

float HectonCoreLitResolveFlashlightShadowFloor()
{
    return max(_HectonFlashlightShadowFloor, 0.02);
}

half HectonCoreLitResolveFlashlightPhotophobia(float3 positionWS)
{
    half fieldPhotophobia = 1.0h;
    if (_HectonPhotophobiaFieldState.x > 0.5 && _HectonPhotophobiaFieldOriginScale.w > 0.000001)
    {
        float2 fieldUv = (positionWS.xz - _HectonPhotophobiaFieldOriginScale.xz) *
            _HectonPhotophobiaFieldOriginScale.w + 0.5;
        if (fieldUv.x >= 0.0 && fieldUv.x <= 1.0 && fieldUv.y >= 0.0 && fieldUv.y <= 1.0)
        {
            float fieldValue = SAMPLE_TEXTURE2D(
                _HectonPhotophobiaFieldTex,
                sampler_HectonPhotophobiaFieldTex,
                fieldUv).r;
            fieldPhotophobia = (half)saturate(fieldValue);
        }
    }

    if (_HectonFlashlightActive <= 0.5)
        return fieldPhotophobia;

    float lightEnergy = saturate(_HectonFlashlightColor.w * 0.12);
    if (lightEnergy <= 0.0001)
        return fieldPhotophobia;

    float3 lightPositionWS = _HectonFlashlightPositionWS.xyz;
    float lightRange = max(_HectonFlashlightPositionWS.w, 0.1);
    float3 toSampleWS = positionWS - lightPositionWS;
    float sampleDistanceSq = dot(toSampleWS, toSampleWS);
    if (sampleDistanceSq <= 0.00000001 || sampleDistanceSq >= lightRange * lightRange)
        return 1.0h;

    float sampleDistance = max(HectonCoreLitApproxDistance(toSampleWS), 0.00001);
    float3 sampleDirectionWS = toSampleWS / sampleDistance;
    float3 lightDirectionWS = HectonCoreLitSafeNormalize(_HectonFlashlightDirectionWS.xyz);
    float innerCos = _HectonFlashlightDirectionWS.w;
    float outerCos = _HectonFlashlightConeData.x;
    float coneRange = max(innerCos - outerCos, 0.0001);
    float coneMask = saturate((dot(lightDirectionWS, sampleDirectionWS) - outerCos) / coneRange);
    float inverseRange = max(_HectonFlashlightConeData.z, 0.0001);
    float rangeMask;
    [branch]
    if (_HectonXRFoveatedParams.x > 0.5)
        rangeMask = saturate(1.0 - sampleDistanceSq * inverseRange * inverseRange);
    else
        rangeMask = saturate(1.0 - sampleDistance * inverseRange);
    rangeMask *= rangeMask;
    float photophobia = coneMask * rangeMask * lightEnergy;
    return (half)(lerp(1.0, 0.0, saturate(photophobia)) * fieldPhotophobia);
}

bool HectonCoreLitIsInsideCaveSolid(float3 positionWS, float surfaceEpsilon);
float HectonCoreLitEvaluateCaveAmbientFactor(float3 positionWS, float3 normalWS);
float HectonCoreLitSampleCaveVoxelSignedDistance(float3 positionWS);

float HectonCoreLitEvaluateDirectionalCausticsWeightFromUnitNormal(float3 normalizedNormalWS)
{
    float3 sunDirection = HectonCoreLitSafeNormalize(_SunDirection.xyz);
    return saturate(dot(normalizedNormalWS, -sunDirection));
}

float HectonCoreLitEvaluateDirectionalCausticsWeight(float3 normalWS)
{
    return HectonCoreLitEvaluateDirectionalCausticsWeightFromUnitNormal(HectonCoreLitSafeNormalize(normalWS));
}

float HectonCoreLitEvaluateCausticsUpMaskFromUnitNormal(float3 normalizedNormalWS)
{
    return saturate(normalizedNormalWS.y * 1.25);
}

float HectonCoreLitEvaluateCausticsUpMask(float3 normalWS)
{
    return HectonCoreLitEvaluateCausticsUpMaskFromUnitNormal(HectonCoreLitSafeNormalize(normalWS));
}

half3 HectonCoreLitSampleUnderwaterCausticsTexture(
    TEXTURE2D_PARAM(causticsTexture, causticsSampler),
    float3 positionWS,
    half3 causticTint,
    half intensity,
    half worldScale,
    float2 scrollVelocity,
    half depthMeters)
{
    float2 uv = positionWS.xz * max((float)worldScale, 0.0001) + scrollVelocity * HectonCoreLitResolveWrappedVisualTime() + _CausticOffset.xy;
    half stripeA = (half)(1.0 - abs(frac(dot(uv, float2(0.73, 0.41))) * 2.0 - 1.0));
    half stripeB = (half)(1.0 - abs(frac(dot(uv, float2(-0.31, 0.91)) + 0.37) * 2.0 - 1.0));
    half caustic = saturate(stripeA * stripeB * 2.0h - 0.28h);
    caustic *= caustic;
    half depthFade = saturate(1.0h - max(depthMeters, 0.0h) * rcp(50.0h));
    return causticTint * caustic * saturate(intensity) * depthFade;
}

half3 HectonCoreLitEvaluateGiantAbyssLightFromUnitNormal(float3 normalizedNormalWS)
{
    float3 aegirDirection = HectonCoreLitSafeNormalize(_AegirDirection.xyz);
    float facing = saturate(dot(normalizedNormalWS, aegirDirection) * 0.5 + 0.5);
    return (half3)(_FinalGiantAbyssLight.rgb * facing);
}

half3 HectonCoreLitEvaluateGiantAbyssLight(float3 normalWS)
{
    return HectonCoreLitEvaluateGiantAbyssLightFromUnitNormal(HectonCoreLitSafeNormalize(normalWS));
}

float HectonCoreLitEvaluateEclipseWaterShadow(float3 positionWS)
{
    float strength = saturate(_HectonEclipseWaterShadowParams.w);
    if (strength <= 0.0001)
        return 1.0;

    float radius = max(_HectonEclipseWaterShadowParams.z, 1.0);
    float softness = saturate(_HectonEclipseWaterShadowDirection.z);
    float innerRadius = radius * saturate(1.0 - softness);
    float2 shadowDelta = positionWS.xz - _HectonEclipseWaterShadowParams.xy;
    float distanceToShadowSq = dot(shadowDelta, shadowDelta);
    float outerRadiusSq = radius * radius;
    float innerRadiusSq = min(innerRadius * innerRadius, outerRadiusSq - 0.0001);
    float shadowMask = 1.0 - HectonCoreLitLinearRamp(innerRadiusSq, outerRadiusSq, distanceToShadowSq);
    return saturate(1.0 - shadowMask * strength);
}

float HectonCoreLitEvaluateRingCausticShadow(float3 positionWS)
{
    return 1.0;
}

float HectonCoreLitEvaluateCelestialWaterShadow(float3 positionWS)
{
    return HectonCoreLitEvaluateEclipseWaterShadow(positionWS) *
           HectonCoreLitEvaluateRingCausticShadow(positionWS);
}

float HectonCoreLitEvaluateMainLightCausticShadow(float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    return saturate(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
}

float HectonCoreLitEvaluateCausticsSceneDepthFade(float3 positionWS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    if (positionCS.w <= 0.0001 || !all(isfinite(positionCS)))
        return 0.0;

    float2 screenUV = HectonCoreLitResolveClipScreenUV(positionCS);
    if (any(screenUV < 0.0) || any(screenUV > 1.0))
        return 0.0;

    float rawDepth = SampleSceneDepth(HectonCoreLitResolveFoveatedSourceUV(screenUV));
#if UNITY_REVERSED_Z
    float depthValid = step(0.0001, rawDepth);
#else
    float depthValid = step(rawDepth, 0.9999);
#endif
    if (depthValid <= 0.5)
        return 0.0;

    float sceneDepthMeters = LinearEyeDepth(rawDepth, _ZBufferParams);
    float sceneDepthFade = 1.0 - HectonCoreLitLinearRamp(18.0, 32.0, sceneDepthMeters);
    float abyssFloorFade = saturate((positionWS.y + 1000.0) * 0.02);
    return saturate(sceneDepthFade * abyssFloorFade);
}

float HectonCoreLitEvaluateCaveAmbientFactorFromSignedDistance(float signedDistance)
{
    float fadeStart = max(_HectonCaveVoxelAoParams.x, 0.001);
    float fadeEnd = max(_HectonCaveVoxelAoParams.y, fadeStart + 0.001);
    float intensity = saturate(_HectonCaveVoxelAoParams.z);
    float floorValue = saturate(_HectonCaveVoxelAoParams.w);
    float wallProximity = 1.0 - HectonCoreLitLinearRamp(fadeStart, fadeEnd, signedDistance);
    float attenuation = saturate(wallProximity * intensity);
    return lerp(1.0, floorValue, attenuation);
}

float HectonCoreLitEvaluateProjectedCausticsMaskFromUnitNormal(float3 positionWS, float3 normalizedNormalWS, out float celestialShadow)
{
    celestialShadow = 1.0;
    return 0.0;
}

float HectonCoreLitEvaluateProjectedCausticsMask(float3 positionWS, float3 normalWS, out float celestialShadow)
{
    celestialShadow = 1.0;
    return 0.0;
}

half3 HectonCoreLitEvaluateProjectedCausticsScattering(float3 positionWS, float3 normalWS)
{
    return half3(0.0h, 0.0h, 0.0h);
}

half HectonCoreLitEvaluateNoirFog(half fogRaw)
{
    half fog = saturate(fogRaw);
    return fog * fog * (0.82h + fog * 0.18h);
}

half3 HectonCoreLitApplyNoirBlackCrush(half3 color, half depth01)
{
    const half3 abyssFloor = half3(0.0015h, 0.0023h, 0.0031h);
    half3 safeColor = max(color, abyssFloor);
    half luma = dot(safeColor, half3(0.2126h, 0.7152h, 0.0722h));
    half shadow01 = saturate((0.055h - luma) * 18.1818h);
    half depthWeight = saturate((depth01 - 0.48h) * 1.9231h);
    half crush = shadow01 * depthWeight;
    half3 crushed = max(safeColor * (1.0h - crush * 0.72h), abyssFloor);
    return lerp(safeColor, crushed, crush);
}

half3 HectonCoreLitApplyDepthCrushCurve(half3 color, float3 positionWS)
{
    const half3 abyssFloor = half3(0.0015h, 0.0023h, 0.0031h);
    float surfaceY = _InternalWaterlineY;
    float depthMeters = max(0.0, surfaceY - positionWS.y);
    half crushWeight = (half)saturate((depthMeters - 500.0) * 0.002);
    half3 safeColor = max(color, abyssFloor);
    half3 cheapCrushed = max(safeColor * safeColor, abyssFloor);
    half3 exactCrushed = max((half3)pow((float3)safeColor, float3(2.2, 2.2, 2.2)), abyssFloor);
    half3 crushed = lerp(cheapCrushed, exactCrushed, (half)HectonCoreLitMathLodWeight());
    return lerp(safeColor, crushed, crushWeight);
}

float3 HectonCoreLitSampleNoirFogLut(float sample01)
{
    if (_HectonNoirFogLutParams.w <= 0.5)
        return 0.0;

    float sampleIndex = saturate(sample01) * (HECTON_NOIR_FOG_LUT_SAMPLE_COUNT - 1);
    int indexA = (int)floor(sampleIndex);
    int indexB = min(indexA + 1, HECTON_NOIR_FOG_LUT_SAMPLE_COUNT - 1);
    float blend = frac(sampleIndex);
    return lerp(_NoirFogLUTSamples[indexA].rgb, _NoirFogLUTSamples[indexB].rgb, blend);
}

float HectonCoreLitEvaluateThermoclineFogMultiplier(float3 positionWS)
{
    float thermoclineY = _HectonNoirFogStratification.x;
    float halfSpan = max(_HectonNoirFogStratification.y, 0.001);
    float abyssalBoost = max(_HectonNoirFogStratification.z, 0.0);
    float fogDensity = max(_HectonNoirFogStratification.w, 0.0001);
    float thermoclineMask = 1.0 - HectonCoreLitLinearRamp(thermoclineY - halfSpan, thermoclineY + halfSpan, positionWS.y);
    float densityPx = fogDensity * (1.0 + thermoclineMask * abyssalBoost);
    return densityPx / fogDensity;
}

half3 HectonCoreLitApplyNoirFog(half3 color, half fogRaw, float3 positionWS)
{
    half fogFactor = HectonCoreLitEvaluateNoirFog(fogRaw);
    float densityMultiplier = HectonCoreLitEvaluateThermoclineFogMultiplier(positionWS);
    float abyssDepthMask = saturate(_H8AbyssAbsorptionColor.w);
    float abyssFogBoost = max(0.0, _H8AbyssAtmosphereParams.x);
    float abyssDetailWeight = saturate(_H8AbyssAtmosphereParams.y);
    float lutSample = saturate(fogFactor * densityMultiplier * (1.0 + abyssFogBoost));
    if (lutSample <= 0.0001)
        return color;

    float3 fogColor = HectonCoreLitSampleNoirFogLut(lutSample);
    float3 abyssFloor = max(_H8AbyssAbsorptionColor.rgb, 0.0) * abyssDepthMask;
    fogColor = max(fogColor, abyssFloor);
    float weatherStress = saturate((1.0 - _HectonWeatherIntensity) + _HectonNoirFogLutBlend * 0.25);
    float wrappedVisualTime = HectonCoreLitResolveWrappedVisualTime();
    float fogPhase =
        wrappedVisualTime * (0.8 + weatherStress * 1.7) +
        positionWS.y * 0.015 +
        dot(positionWS.xz, float2(0.007, -0.009));
    float fogPulse = HectonCoreLitTrianglePulse01(fogPhase);
    float fogShimmer = HectonCoreLitValueNoise2(positionWS.xz * 0.043 + wrappedVisualTime * float2(0.031, -0.024));
    float pressureSpark = saturate((fogShimmer - 0.58) * 3.3) * weatherStress * saturate(lutSample) * lerp(0.55, 1.0, abyssDetailWeight);
    fogColor *= 1.0 + fogPulse * weatherStress * 0.055;
    fogColor += _FinalGiantAbyssLight.rgb * (fogPulse * weatherStress * 0.06);
    fogColor += (fogColor + _FinalGiantAbyssLight.rgb * 0.5 + abyssFloor * 0.65) * pressureSpark * 0.045;
    float chromaDrift = (fogShimmer - 0.5) * weatherStress * saturate(lutSample) * 0.028;
    fogColor = max(fogColor + float3(-chromaDrift, chromaDrift * 0.45, chromaDrift * 0.8), 0.0);
    float blackoutBand = HectonCoreLitLinearRamp(0.87, 1.0, HectonCoreLitTrianglePulse01(positionWS.y * 0.026 + wrappedVisualTime * 0.41 + fogShimmer * 3.1));
    fogColor *= 1.0 - blackoutBand * weatherStress * saturate(lutSample) * 0.032;
    float3 absorption = max(fogColor * float3(0.72, 0.52, 0.36), abyssFloor * float3(0.34, 0.28, 0.22));
    float3 ambientTint = fogColor * 0.42;
    float3 absorptionX = absorption * lutSample;
    float3 attenuatedColor = color * rcp(1.0 + absorptionX + absorptionX * absorptionX * 0.5);
    float3 fogTarget = fogColor + ambientTint * 0.35 + _FinalGiantAbyssLight.rgb * (0.18 * saturate(lutSample));
    half3 foggedColor = (half3)lerp(attenuatedColor, fogTarget, saturate(lutSample));
    foggedColor = HectonCoreLitApplyNoirBlackCrush(foggedColor, (half)lutSample);
    return HectonCoreLitApplyDepthCrushCurve(foggedColor, positionWS);
}

half HectonCoreLitEvaluateOrganicSssScalar(
    float3 viewDirWS,
    float3 lightDirWS,
    float3 normalWS,
    half distortion,
    half power,
    half scale)
{
    half resolvedScale = max(scale, 0.0h);
    if (resolvedScale <= 0.0001h)
        return 0.0h;

    const half wrap = 0.5h;
    float diffuse = max(0.0, dot(normalWS, lightDirWS) + wrap) / (1.0h + wrap);
    return (half)(diffuse * resolvedScale);
}

half3 HectonCoreLitEvaluateOrganicSss(
    float3 viewDirWS,
    float3 lightDirWS,
    float3 normalWS,
    half3 sssColor,
    half distortion,
    half power,
    half scale)
{
    half colorEnergy = max(max(sssColor.r, sssColor.g), sssColor.b);
    if (colorEnergy <= 0.0001h)
        return half3(0.0h, 0.0h, 0.0h);

    return sssColor * HectonCoreLitEvaluateOrganicSssScalar(viewDirWS, lightDirWS, normalWS, distortion, power, scale);
}

float HectonCoreLitEvaluateActiveSonarTriplanarGrid(float3 positionWS, float gridEnabled)
{
    float mathLodWeight = HectonCoreLitMathLodWeight();
    if (mathLodWeight <= 0.0001)
        return 1.0;

    if (gridEnabled <= 0.5)
        return 1.0;

    float3 stablePosition = positionWS + _TotalUniverseOffset.xyz;
    float2 uvXY = stablePosition.xy * 0.085 + float2(13.7, 29.1);
    float2 uvYZ = stablePosition.yz * 0.085 + float2(41.3, 7.9);
    float2 uvZX = stablePosition.zx * 0.085 + float2(19.5, 53.2);
    float2 cellXY = abs(frac(uvXY) - 0.5);
    float2 cellYZ = abs(frac(uvYZ) - 0.5);
    float2 cellZX = abs(frac(uvZX) - 0.5);
    float gridXY = 1.0 - saturate(min(cellXY.x, cellXY.y) * 26.0);
    float gridYZ = 1.0 - saturate(min(cellYZ.x, cellYZ.y) * 26.0);
    float gridZX = 1.0 - saturate(min(cellZX.x, cellZX.y) * 26.0);
    float grid = max(max(gridXY, gridYZ), gridZX);
    if (gridEnabled > 1.5)
    {
        float2 fineCell = abs(frac((stablePosition.xy + stablePosition.zz * 0.37) * 0.17 + float2(5.1, 2.7)) - 0.5);
        float fineGrid = 1.0 - saturate(min(fineCell.x, fineCell.y) * 42.0);
        float rib = 1.0 - saturate(abs(frac(dot(stablePosition, float3(0.019, 0.031, 0.043))) - 0.5) * 10.0);
        grid = saturate(max(grid, fineGrid * 0.72) + rib * 0.18);
    }

    float scanNoise = abs(frac(dot(stablePosition, float3(0.037, 0.011, 0.029))) - 0.5) * 2.0;
    return lerp(1.0, saturate(0.62 + grid * 0.55 + (scanNoise - 0.5) * 0.18), mathLodWeight);
}

float HectonCoreLitEvaluateActiveSonarGeoRing(float3 positionWS)
{
    int pingCount = clamp((int)_ActiveSonarGeoParams.x, 0, HECTON_ACTIVE_SONAR_MAX_PINGS);
    if (pingCount <= 0)
        return 0.0;

    float maxRange = max(_ActiveSonarGeoParams.y, 1.0);
    float gridEnabled = _ActiveSonarGeoParams.z;
    float ringAccum = 0.0;
    [unroll]
    for (int pingIndex = 0; pingIndex < HECTON_ACTIVE_SONAR_MAX_PINGS; pingIndex++)
    {
        if (pingIndex >= pingCount)
            break;

        float4 centerRadius = _ActiveSonarCentersRadius[pingIndex];
        float radius = max(centerRadius.w, 0.0);
        if (radius <= 0.0001 || radius >= maxRange)
            continue;

        float3 delta = positionWS - centerRadius.xyz;
        float distSq = dot(delta, delta);
        float radiusSq = radius * radius;
        float ring = 1.0 - saturate(abs(distSq - radiusSq) * 0.05);
        float fade = 1.0 - saturate(radius * rcp(maxRange));
        float intensity = saturate(_ActiveSonarParams[pingIndex].x);
        ringAccum = max(ringAccum, ring * fade * intensity);
    }

    float grid = HectonCoreLitEvaluateActiveSonarTriplanarGrid(positionWS, gridEnabled);
    return saturate(ringAccum * grid);
}

float3 HectonCoreLitEvaluateActiveSonarGeoEmission(float3 positionWS)
{
    float ring = HectonCoreLitEvaluateActiveSonarGeoRing(positionWS);
    return float3(0.03, 1.25, 1.65) * ring;
}

float HectonCoreLitEvaluateSonarReactiveBiolumBoost(float3 positionWS)
{
    if (_Time.y > _SonarRevealExpireTime)
        return 0.0;

    float sonarPulseIntensity = saturate(_SonarRevealWaveParams.w);
    if (sonarPulseIntensity <= 0.0001)
        return 0.0;

    float revealRadius = max(_SonarRevealOriginWS.w, 0.0);
    if (revealRadius <= 0.0)
        return 0.0;

    float3 originDelta = positionWS - _SonarRevealOriginWS.xyz;
    float distanceToOriginSq = dot(originDelta, originDelta);
    float revealRadiusSq = revealRadius * revealRadius;
    if (distanceToOriginSq > revealRadiusSq)
        return 0.0;

    float distanceToOrigin = HectonCoreLitApproxDistance(originDelta);
    float sonarWaveSpeed = max(_SonarRevealWaveParams.y, 0.01);
    float sonarFadeDuration = max(_SonarRevealWaveParams.z, 0.05);
    float arrivalTime = _SonarRevealWaveParams.x + distanceToOrigin / sonarWaveSpeed;
    float timeSinceArrival = _Time.y - arrivalTime;
    float decay = 1.0 - saturate(max(timeSinceArrival, 0.0) / sonarFadeDuration);
    float waveRadius = max(0.0, _SonarWaveFront);
    float waveBandWidth = lerp(6.0, 2.0, sonarPulseIntensity);
    float waveBand = 1.0 - saturate(abs(distanceToOrigin - waveRadius) / max(waveBandWidth, 0.25));
    float active = step(_Time.y, _SonarRevealExpireTime);
    return active * max(decay, waveBand * sonarPulseIntensity) * sonarPulseIntensity;
}

float3 HectonCoreLitEvaluateGlowPointRadiance(float3 positionWS)
{
    float3 activeSonarGeoEmission = HectonCoreLitEvaluateActiveSonarGeoEmission(positionWS);
    int glowCount = clamp((int)round(_HectonGlowPointParams.x), 0, HECTON_GLOW_POINT_MAX);
    if (glowCount <= 0)
        return activeSonarGeoEmission;

    float3 radiance = 0.0;
    [unroll]
    for (int glowIndex = 0; glowIndex < HECTON_GLOW_POINT_MAX; glowIndex++)
    {
        if (glowIndex >= glowCount)
            break;

        float4 positionRange = _HectonGlowPointPositionRange[glowIndex];
        float range = max(positionRange.w, 0.001);
        float rangeSq = range * range;
        float3 delta = positionWS - positionRange.xyz;
        float distanceSq = dot(delta, delta);
        float insideRange = step(distanceSq, rangeSq);
        float falloff = saturate(1.0 - distanceSq * rcp(rangeSq));
        falloff *= falloff;

        float4 colorIntensity = _HectonGlowPointColorIntensity[glowIndex];
        radiance += colorIntensity.rgb * max(colorIntensity.w, 0.0) * falloff * insideRange;
    }

    float sonarReactiveBoost = HectonCoreLitEvaluateSonarReactiveBiolumBoost(positionWS);
    return activeSonarGeoEmission + radiance * (1.0 + sonarReactiveBoost * max(_HectonGlowPointParams.y, 0.0));
}

float HectonCoreLitSampleCaveVoxelSignedDistance(float3 positionWS)
{
    if (_HectonCaveVoxelActive <= 0.5)
        return _HectonCaveVoxelHalfExtents.w;

    float3 invDoubleHalfExtents = _HectonCaveVoxelInvDoubleHalfExtents.xyz;
    if (any(invDoubleHalfExtents <= 0.0))
        return _HectonCaveVoxelHalfExtents.w;

    float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
    float3 sampleUv = localPosition * invDoubleHalfExtents + 0.5;
    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
        return _HectonCaveVoxelHalfExtents.w;

    float encoded = SAMPLE_TEXTURE3D_LOD(_HectonCaveVoxelSdfTex, sampler_HectonCaveVoxelSdfTex, sampleUv, 0).r;
    return lerp(-_HectonCaveVoxelHalfExtents.w, _HectonCaveVoxelHalfExtents.w, encoded);
}

float HectonCoreLitEvaluateCaveAmbientFactor(float3 positionWS, float3 normalWS)
{
    if (_HectonCaveVoxelActive <= 0.5)
        return 1.0;

    float aoIntensity = saturate(_HectonCaveVoxelAoParams.z);
    float aoFloor = saturate(_HectonCaveVoxelAoParams.w);
    if (aoIntensity <= 0.0001 || aoFloor >= 0.999)
        return 1.0;

    float signedDistance = HectonCoreLitSampleCaveVoxelSignedDistance(positionWS + normalWS * 0.03);
    return HectonCoreLitEvaluateCaveAmbientFactorFromSignedDistance(signedDistance);
}

float HectonCoreLitEvaluateScreenSpaceContactShadowFromUnitLightDirection(
    float3 surfacePositionWS,
    float3 normalWS,
    float3 normalizedLightDirectionWS,
    float maxShadowDistance)
{
    if (_HectonContactShadowStrength <= 0.0001 || maxShadowDistance <= 0.0001)
        return 1.0;

    float noL = saturate(dot(normalWS, normalizedLightDirectionWS));
    if (noL <= 0.0001)
        return 1.0;

    float4 surfaceCS = TransformWorldToHClip(surfacePositionWS);
    if (surfaceCS.w <= 0.0001 || !all(isfinite(surfaceCS)))
        return 1.0;

    float3 biasedSurfacePositionWS = surfacePositionWS + normalWS * max(_HectonContactShadowBias, 0.001);
    const int stepCount = 4;
    float jitter = HectonCoreLitInterleavedGradientNoise(surfaceCS.xy);
    float shadowOcclusion = 0.0;

    [unroll]
    for (int stepIndex = 0; stepIndex < 4; stepIndex++)
    {
        float stepT = (stepIndex + 0.5 + jitter * 0.35) * 0.25;
        float3 raySampleWS = biasedSurfacePositionWS + normalizedLightDirectionWS * (maxShadowDistance * stepT);
        float4 raySampleCS = TransformWorldToHClip(raySampleWS);
        if (raySampleCS.w <= 0.0 || !all(isfinite(raySampleCS)))
            continue;

        float2 raySampleUV = raySampleCS.xy * rcp(raySampleCS.w) * 0.5 + 0.5;
        if (raySampleUV.x <= 0.0 || raySampleUV.x >= 1.0 || raySampleUV.y <= 0.0 || raySampleUV.y >= 1.0)
            continue;

        float distanceWeight = 1.0 - stepT;
        float occluded = distanceWeight * step(jitter * 0.25, 0.18 + noL * 0.82);
        shadowOcclusion = max(shadowOcclusion, occluded * noL);
    }

    return lerp(1.0, 0.2, saturate(shadowOcclusion * _HectonContactShadowStrength));
}

float HectonCoreLitEvaluateScreenSpaceContactShadow(
    float3 surfacePositionWS,
    float3 normalWS,
    float3 lightDirectionWS,
    float maxShadowDistance)
{
    return HectonCoreLitEvaluateScreenSpaceContactShadowFromUnitLightDirection(
        surfacePositionWS,
        normalWS,
        HectonCoreLitSafeNormalize(lightDirectionWS),
        maxShadowDistance);
}

float HectonCoreLitEvaluateMainLightContactShadowFromDirection(float3 surfacePositionWS, float3 normalWS, float3 mainLightDirectionWS)
{
    return HectonCoreLitEvaluateScreenSpaceContactShadowFromUnitLightDirection(
        surfacePositionWS,
        normalWS,
        HectonCoreLitSafeNormalize(mainLightDirectionWS),
        _HectonContactShadowMaxDistance);
}

float HectonCoreLitEvaluateMainLightContactShadow(float3 surfacePositionWS, float3 normalWS)
{
    Light mainLight = GetMainLight();
    return HectonCoreLitEvaluateMainLightContactShadowFromDirection(surfacePositionWS, normalWS, mainLight.direction);
}

float3 HectonCoreLitSampleBiolumVolumeRadiance(float3 positionWS)
{
    float3 glowPointRadiance = HectonCoreLitEvaluateGlowPointRadiance(positionWS);
    if (_HectonBiolumVolumeActive <= 0.5)
        return glowPointRadiance;

    float intensity = max(_HectonBiolumVolumeParams.x, 0.0);
    if (intensity <= 0.0001)
        return glowPointRadiance;

    float3 halfExtents = max(_HectonBiolumVolumeHalfExtents.xyz, float3(0.001, 0.001, 0.001));
    float3 localPosition = mul(_HectonBiolumVolumeWorldToLocal, float4(positionWS, 1.0)).xyz;
    float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
        return glowPointRadiance;

    float4 volumeSample = SAMPLE_TEXTURE3D_LOD(_HectonBiolumVolumeTex, sampler_HectonBiolumVolumeTex, sampleUv, 0);
    if (max(max(volumeSample.r, volumeSample.g), volumeSample.b) <= 0.0001)
        return glowPointRadiance;

    float sonarReactiveBoost = HectonCoreLitEvaluateSonarReactiveBiolumBoost(positionWS);
    float breathPhase = HectonCoreLitResolveWrappedVisualTime() * 1.2566371 + (positionWS.x + _TotalUniverseOffset.x) * 0.013 + positionWS.z * -0.017;
    float biolumBreath = 0.92 + 0.08 * (HectonCoreLitTrianglePulse01(breathPhase) * 2.0 - 1.0);
    return glowPointRadiance + volumeSample.rgb * intensity * biolumBreath * (1.0 + sonarReactiveBoost * 2.5);
}

bool HectonCoreLitIsInsideCaveSolid(float3 positionWS, float surfaceEpsilon)
{
    return _HectonCaveVoxelActive > 0.5 && HectonCoreLitSampleCaveVoxelSignedDistance(positionWS) <= surfaceEpsilon;
}

float HectonCoreLitSampleFlashlightSignedDistance(float3 positionWS)
{
    if (_HectonFlashlightVoxelActive <= 0.5)
        return _HectonFlashlightVoxelHalfExtents.w;

    float3 halfExtents = max(_HectonFlashlightVoxelHalfExtents.xyz, float3(0.001, 0.001, 0.001));
    float3 localPosition = mul(_HectonFlashlightVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
    float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
        return _HectonFlashlightVoxelHalfExtents.w;

    float encoded = SAMPLE_TEXTURE3D_LOD(_VoxelDensityTex, sampler_VoxelDensityTex, sampleUv, 0).r;
    return lerp(-_HectonFlashlightVoxelHalfExtents.w, _HectonFlashlightVoxelHalfExtents.w, encoded);
}

float HectonCoreLitEvaluateFlashlightShadow(float3 surfacePositionWS, float3 normalWS)
{
    if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
        return 1.0;

    float shadowFloor = HectonCoreLitResolveFlashlightShadowFloor();
    if (shadowFloor >= 0.999)
        return 1.0;

    float3 lightVector = _HectonFlashlightPositionWS.xyz - surfacePositionWS;
    float lightDistanceSq = dot(lightVector, lightVector);
    if (lightDistanceSq <= 0.00000001)
        return 1.0;

    float lightDistance = max(HectonCoreLitApproxDistance(lightVector), 0.00001);
    float shadowBias = max(_HectonFlashlightShadowBias, 0.001);
    float rayLength = max(lightDistance - shadowBias, 0.0);
    if (rayLength <= 0.0001)
        return 1.0;

    float3 rayDirectionWS = lightVector / lightDistance;
    float3 rayOriginWS = surfacePositionWS + HectonCoreLitSafeNormalize(normalWS) * shadowBias;
    const int maxVoxelShadowSteps = HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS;
    int stepCount = min(maxVoxelShadowSteps, clamp(HectonCoreLitRoundToIntFast(_HectonFlashlightShadowSteps), 1, maxVoxelShadowSteps));
    float minStep = max(_HectonFlashlightShadowMinStep, 0.01);
    float res = 1.0;
    float t = minStep;

    [loop]
    for (int stepIndex = 0; stepIndex < maxVoxelShadowSteps; ++stepIndex)
    {
        if (stepIndex >= stepCount || t >= rayLength)
            break;

        float3 samplePositionWS = rayOriginWS + rayDirectionWS * t;
        float h = HectonCoreLitSampleFlashlightSignedDistance(samplePositionWS);
        if (h <= 0.0001)
            return shadowFloor;

        res = min(res, _HectonFlashlightShadowSoftness * h / max(t, 0.001));
        t += max(h, minStep);
    }

    return saturate(max(res, shadowFloor));
}

bool HectonCoreLitTryResolveAdditionalLight(uint lightLoopIndex, out float3 lightPositionWS, out float3 spotDirectionWS)
{
#if USE_CLUSTER_LIGHT_LOOP
    int lightIndex = lightLoopIndex;
#else
    int lightIndex = GetPerObjectLightIndex(lightLoopIndex);
#endif

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 lightPosition = _AdditionalLightsBuffer[lightIndex].position;
    half4 spotDirection = _AdditionalLightsBuffer[lightIndex].spotDirection;
#else
    float4 lightPosition = _AdditionalLightsPosition[lightIndex];
    half4 spotDirection = _AdditionalLightsSpotDir[lightIndex];
#endif

    lightPositionWS = lightPosition.xyz;
    spotDirectionWS = spotDirection.xyz;
    return lightPosition.w > 0.5;
}

float HectonCoreLitEvaluateAdditionalLightContactShadowFromResolved(float3 additionalLightPositionWS, float3 positionWS, float3 normalWS, float defaultShadowAttenuation)
{
    if (defaultShadowAttenuation <= 0.0001 || _HectonContactShadowStrength <= 0.0001 || _HectonContactShadowMaxDistance <= 0.0001)
        return defaultShadowAttenuation;

    float3 lightVectorWS = additionalLightPositionWS - positionWS;
    float lightDistanceSq = dot(lightVectorWS, lightVectorWS);
    if (lightDistanceSq <= 0.0001)
        return defaultShadowAttenuation;

    float lightDistance = max(HectonCoreLitApproxDistance(lightVectorWS), 0.0001);
    float3 lightDirectionWS = lightVectorWS / lightDistance;
    float maxDistance = min(lightDistance, max(_HectonContactShadowMaxDistance, 0.001));
    float contactShadow = HectonCoreLitEvaluateScreenSpaceContactShadowFromUnitLightDirection(positionWS, normalWS, lightDirectionWS, maxDistance);
    return min(defaultShadowAttenuation, contactShadow);
}

float HectonCoreLitEvaluateAdditionalLightContactShadow(uint lightLoopIndex, float3 positionWS, float3 normalWS, float defaultShadowAttenuation)
{
    if (defaultShadowAttenuation <= 0.0001 || _HectonContactShadowStrength <= 0.0001 || _HectonContactShadowMaxDistance <= 0.0001)
        return defaultShadowAttenuation;

    float3 additionalLightPositionWS;
    float3 additionalSpotDirectionWS;
    if (!HectonCoreLitTryResolveAdditionalLight(lightLoopIndex, additionalLightPositionWS, additionalSpotDirectionWS))
        return defaultShadowAttenuation;

    return HectonCoreLitEvaluateAdditionalLightContactShadowFromResolved(
        additionalLightPositionWS,
        positionWS,
        normalWS,
        defaultShadowAttenuation);
}

float HectonCoreLitResolveFlashlightAdditionalShadow(uint lightLoopIndex, float3 positionWS, float3 normalWS, float defaultShadowAttenuation)
{
    if (defaultShadowAttenuation <= 0.0001)
        return defaultShadowAttenuation;

    bool contactShadowEnabled = _HectonContactShadowStrength > 0.0001 && _HectonContactShadowMaxDistance > 0.0001;
    bool flashlightShadowEnabled = _HectonFlashlightActive > 0.5 && _HectonFlashlightVoxelActive > 0.5;
    if (!contactShadowEnabled && !flashlightShadowEnabled)
        return defaultShadowAttenuation;

    float3 additionalLightPositionWS;
    float3 additionalSpotDirectionWS;
    if (!HectonCoreLitTryResolveAdditionalLight(lightLoopIndex, additionalLightPositionWS, additionalSpotDirectionWS))
        return defaultShadowAttenuation;

    float contactShadowAttenuation = contactShadowEnabled
        ? HectonCoreLitEvaluateAdditionalLightContactShadowFromResolved(additionalLightPositionWS, positionWS, normalWS, defaultShadowAttenuation)
        : defaultShadowAttenuation;

    if (contactShadowAttenuation <= 0.0001)
        return contactShadowAttenuation;

    if (!flashlightShadowEnabled)
        return contactShadowAttenuation;

    float3 positionDelta = additionalLightPositionWS - _HectonFlashlightPositionWS.xyz;
    if (dot(positionDelta, positionDelta) > 0.0625)
        return contactShadowAttenuation;

    float directionMatch = dot(
        HectonCoreLitSafeNormalize(additionalSpotDirectionWS),
        HectonCoreLitSafeNormalize(_HectonFlashlightDirectionWS.xyz));
    if (directionMatch < 0.98)
        return contactShadowAttenuation;

    return min(contactShadowAttenuation, HectonCoreLitEvaluateFlashlightShadow(positionWS, normalWS));
}

#endif
