#ifndef HECTON_CORE_LIT_INCLUDED
#define HECTON_CORE_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#ifndef HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS
#define HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS 24
#endif

#ifndef HECTON_PARASITE_MAX_ANCHORS
#define HECTON_PARASITE_MAX_ANCHORS 16
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
float4x4 _HectonCaveVoxelWorldToLocal;
float4 _HectonCaveVoxelAoParams;
float4 _HectonBiolumVolumeHalfExtents;
float4 _HectonBiolumVolumeParams;
float4 _HectonProjectedCausticsWorldRect;
float4 _HectonProjectedCausticsParams;
float4 _HectonProjectedCausticsColor;
float4 _HectonCausticsSimulationParamsA;
float4 _HectonCausticsSimulationParamsB;
float4 _HectonCausticsSimulationParamsC;
float4 _SonarRevealOriginWS;
float4 _SonarRevealWaveParams;
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
TEXTURE2D(_NoirFogLUT);
SAMPLER(sampler_NoirFogLUT);
TEXTURE2D(_HectonSedimentMaskTex);
SAMPLER(sampler_HectonSedimentMaskTex);
float4 _HectonNoirFogLutParams;
float _HectonNoirFogLutBlend;
float4 _HectonNoirFogStratification;
float4 _HectonSedimentWorldRect;
float4 _HectonSedimentOverlayParamsA;
float4 _HectonSedimentOverlayParamsB;
float4 _HectonSedimentTintA;
float4 _HectonSedimentTintB;
float4 _HectonParasiteAnchorData[HECTON_PARASITE_MAX_ANCHORS];
float4 _HectonParasiteAnchorParams[HECTON_PARASITE_MAX_ANCHORS];
float4 _HectonParasiteGlobals;

float3 HectonCoreLitSafeNormalize(float3 value)
{
    float lenSq = dot(value, value);
    return lenSq > 0.0001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
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

float HectonCoreLitSedimentRippleHeight(float2 uv)
{
    float layerA = sin(uv.x * 1.73 + uv.y * 0.47);
    float layerB = cos(uv.y * 1.91 - uv.x * 0.29);
    float layerC = sin((uv.x + uv.y) * 0.63 + 1.7);
    return layerA * 0.5 + layerB * 0.35 + layerC * 0.15;
}

float HectonCoreLitSampleSedimentMask(float3 positionWS)
{
    if (_HectonSedimentOverlayParamsA.x <= 0.5)
        return 0.0;

    float2 uv = float2(
        (positionWS.x - _HectonSedimentWorldRect.x) * _HectonSedimentWorldRect.z,
        (positionWS.z - _HectonSedimentWorldRect.y) * _HectonSedimentWorldRect.w);
    if (any(uv < 0.0) || any(uv > 1.0))
        return 0.0;

    return SAMPLE_TEXTURE2D_LOD(_HectonSedimentMaskTex, sampler_HectonSedimentMaskTex, uv, 0).r * _HectonSedimentOverlayParamsB.w;
}

void HectonCoreLitApplySedimentOverlay(
    float3 positionWS,
    inout half3 normalWS,
    inout half3 albedo,
    inout half metallic,
    inout half smoothness)
{
    float sedimentMask = HectonCoreLitSampleSedimentMask(positionWS);
    float upFacing = saturate((normalWS.y - _HectonSedimentOverlayParamsA.y) * _HectonSedimentOverlayParamsA.z);
    float strength = saturate(sedimentMask * upFacing);
    if (strength <= 0.0001)
        return;

    float2 rippleUv = positionWS.xz * _HectonSedimentOverlayParamsA.w;
    float baseHeight = HectonCoreLitSedimentRippleHeight(rippleUv);
    float heightDx = HectonCoreLitSedimentRippleHeight(rippleUv + float2(0.09, 0.0));
    float heightDy = HectonCoreLitSedimentRippleHeight(rippleUv + float2(0.0, 0.09));
    float2 gradient = float2(heightDx - baseHeight, heightDy - baseHeight) * _HectonSedimentOverlayParamsB.x;

    float3 baseNormal = HectonCoreLitSafeNormalize(normalWS);
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
        float distanceToAnchor = distance(positionWS, anchor.xyz);
        float normalizedDistance = 1.0 - saturate(distanceToAnchor / radius);
        float spread = pow(saturate(normalizedDistance), feather);
        float candidateMask = spread * saturate(parameters.x);
        if (candidateMask <= bestMask)
            continue;

        bestMask = candidateMask;
        pulseMultiplier = 1.0 + sin(timeValue * max(parameters.y, 0.05) * 6.2831853 + distanceToAnchor * 0.35) * pulseAmplitude;
        thermalGrowthMask = saturate(parameters.z);
    }

    return saturate(bestMask);
}

float HectonCoreLitVoronoiRidge(float2 uv, float cellDensity, float timePhase)
{
    float2 domain = uv * cellDensity;
    float2 baseCell = floor(domain);
    float2 fracCell = frac(domain);
    float nearest = 9999.0;
    float secondNearest = 9999.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y);
            float2 cell = baseCell + offset;
            float2 jitter = HectonCoreLitHash22(cell);
            float2 animatedPoint = offset + jitter - fracCell;
            animatedPoint += float2(
                sin((jitter.x + timePhase) * 6.2831853),
                cos((jitter.y + timePhase * 1.37) * 6.2831853)) * 0.12;

            float distanceSq = dot(animatedPoint, animatedPoint);
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

    return saturate(sqrt(secondNearest) - sqrt(nearest));
}

float HectonCoreLitEvaluateProceduralCaustics(float2 uv)
{
    float primaryDensity = max(_HectonCausticsSimulationParamsA.x, 0.5);
    float secondaryDensity = max(_HectonCausticsSimulationParamsA.y, primaryDensity);
    float primarySpeed = _HectonCausticsSimulationParamsA.z;
    float secondarySpeed = _HectonCausticsSimulationParamsA.w;
    float sharpness = max(_HectonCausticsSimulationParamsB.x, 0.1);
    float secondaryWeight = saturate(_HectonCausticsSimulationParamsB.y);
    float timeValue = _HectonCausticsSimulationParamsB.z;
    float waveDisplacement = _HectonCausticsSimulationParamsC.x;
    float2 waveFlow = _HectonCausticsSimulationParamsC.yz;
    float wavePhase = _HectonCausticsSimulationParamsC.w;
    float waveDisplacementAbs = abs(waveDisplacement);
    float2 waveOffset = waveFlow * 0.0125 + float2(waveDisplacement * 0.018, -waveDisplacement * 0.013);
    float2 animatedUv = uv + waveOffset;

    primaryDensity *= lerp(0.94, 1.08, saturate(waveDisplacementAbs * 0.2));
    secondaryDensity *= lerp(0.92, 1.12, saturate(length(waveFlow) * 0.08));

    float primaryTime = timeValue * primarySpeed + wavePhase;
    float secondaryTime = timeValue * secondarySpeed + wavePhase * 1.37 + 17.0;
    float primaryLayer = HectonCoreLitVoronoiRidge(animatedUv + float2(primaryTime, -primaryTime * 0.61), primaryDensity, primaryTime);
    float secondaryLayer = HectonCoreLitVoronoiRidge(animatedUv + float2(-secondaryTime * 0.73, secondaryTime), secondaryDensity, secondaryTime);
    float combined = lerp(primaryLayer, max(primaryLayer, secondaryLayer), secondaryWeight);
    combined = pow(saturate(combined * 2.3), sharpness);
    combined *= lerp(0.92, 1.18, saturate(waveDisplacementAbs * 0.14 + length(waveFlow) * 0.035));
    return saturate(combined * 1.35);
}

float HectonCoreLitResolveFlashlightShadowFloor()
{
    return max(_HectonFlashlightShadowFloor, 0.02);
}

bool HectonCoreLitIsInsideCaveSolid(float3 positionWS, float surfaceEpsilon);
float HectonCoreLitEvaluateCaveAmbientFactor(float3 positionWS, float3 normalWS);

float HectonCoreLitEvaluateProjectedCausticsMask(float3 positionWS, float3 normalWS)
{
    if (_HectonProjectedCausticsParams.x <= 0.0001)
        return 0.0;

    float2 uv = float2(
        (positionWS.x - _HectonProjectedCausticsWorldRect.x) * _HectonProjectedCausticsWorldRect.z,
        (positionWS.z - _HectonProjectedCausticsWorldRect.y) * _HectonProjectedCausticsWorldRect.w);
    if (any(uv < 0.0) || any(uv > 1.0))
        return 0.0;

    float depthBelowWater = max(0.0, _HectonProjectedCausticsParams.y - positionWS.y);
    float depthFade = 1.0 - saturate((depthBelowWater - _HectonProjectedCausticsParams.z) * _HectonProjectedCausticsParams.w);
    if (depthFade <= 0.0)
        return 0.0;

    if (HectonCoreLitIsInsideCaveSolid(positionWS, 0.02))
        return 0.0;

    float upFacing = saturate(normalWS.y * 1.25);
    float caustics = HectonCoreLitEvaluateProceduralCaustics(uv);
    float shadowTerm = HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
    return caustics * depthFade * upFacing * shadowTerm * _HectonProjectedCausticsParams.x;
}

half3 HectonCoreLitEvaluateProjectedCausticsScattering(float3 positionWS, float3 normalWS)
{
    float mask = HectonCoreLitEvaluateProjectedCausticsMask(positionWS, normalWS);
    return (half3)(_HectonProjectedCausticsColor.rgb * mask);
}

half HectonCoreLitEvaluateNoirFog(half fogRaw)
{
    return pow(saturate(fogRaw), 2.2h);
}

float3 HectonCoreLitSampleNoirFogLutRow(float rowIndex, float sample01)
{
    if (_HectonNoirFogLutParams.w <= 0.5)
        return 0.0;

    float rowCount = max(_HectonNoirFogLutParams.y, 1.0);
    float2 uv = float2(
        saturate(sample01),
        (rowIndex + 0.5) / rowCount);
    return SAMPLE_TEXTURE2D_LOD(_NoirFogLUT, sampler_NoirFogLUT, uv, 0).rgb;
}

float HectonCoreLitEvaluateThermoclineFogMultiplier(float3 positionWS)
{
    float thermoclineY = _HectonNoirFogStratification.x;
    float halfSpan = max(_HectonNoirFogStratification.y, 0.001);
    float abyssalBoost = max(_HectonNoirFogStratification.z, 0.0);
    float fogDensity = max(_HectonNoirFogStratification.w, 0.0001);
    float thermoclineMask = 1.0 - smoothstep(thermoclineY - halfSpan, thermoclineY + halfSpan, positionWS.y);
    float densityPx = fogDensity * (1.0 + thermoclineMask * abyssalBoost);
    return densityPx / fogDensity;
}

half3 HectonCoreLitApplyNoirFog(half3 color, half fogRaw, float3 positionWS)
{
    half fogFactor = HectonCoreLitEvaluateNoirFog(fogRaw);
    float densityMultiplier = HectonCoreLitEvaluateThermoclineFogMultiplier(positionWS);
    float lutSample = saturate(fogFactor * densityMultiplier);
    float lutBlend = saturate(_HectonNoirFogLutBlend);

    float3 sourceFog = HectonCoreLitSampleNoirFogLutRow(0.0, lutSample);
    float3 sourceAbsorption = HectonCoreLitSampleNoirFogLutRow(1.0, lutSample);
    float3 sourceAmbient = HectonCoreLitSampleNoirFogLutRow(2.0, lutSample);
    float3 targetFog = HectonCoreLitSampleNoirFogLutRow(3.0, lutSample);
    float3 targetAbsorption = HectonCoreLitSampleNoirFogLutRow(4.0, lutSample);
    float3 targetAmbient = HectonCoreLitSampleNoirFogLutRow(5.0, lutSample);

    float3 fogColor = lerp(sourceFog, targetFog, lutBlend);
    float3 absorption = max(lerp(sourceAbsorption, targetAbsorption, lutBlend), float3(0.0, 0.0, 0.0));
    float3 ambientTint = lerp(sourceAmbient, targetAmbient, lutBlend);
    float3 attenuatedColor = color * exp(-absorption * lutSample);
    float3 fogTarget = fogColor + ambientTint * 0.35;
    return (half3)lerp(attenuatedColor, fogTarget, saturate(lutSample));
}

half HectonCoreLitEvaluateOrganicSssScalar(
    float3 viewDirWS,
    float3 lightDirWS,
    float3 normalWS,
    half distortion,
    half power,
    half scale)
{
    float3 normalizedView = HectonCoreLitSafeNormalize(viewDirWS);
    float3 normalizedLight = HectonCoreLitSafeNormalize(lightDirWS);
    float3 normalizedNormal = HectonCoreLitSafeNormalize(normalWS);
    float distortedBacklight = saturate(dot(normalizedView, -(normalizedLight + normalizedNormal * distortion)));
    return (half)(pow(distortedBacklight, max((float)power, 0.001)) * max((float)scale, 0.0));
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
    return sssColor * HectonCoreLitEvaluateOrganicSssScalar(viewDirWS, lightDirWS, normalWS, distortion, power, scale);
}

float HectonCoreLitEvaluateSonarReactiveBiolumBoost(float3 positionWS)
{
    if (_Time.y > _SonarRevealExpireTime)
        return 0.0;

    float revealRadius = max(_SonarRevealOriginWS.w, 0.0);
    if (revealRadius <= 0.0)
        return 0.0;

    float distanceToOrigin = distance(positionWS, _SonarRevealOriginWS.xyz);
    if (distanceToOrigin > revealRadius)
        return 0.0;

    float sonarWaveSpeed = max(_SonarRevealWaveParams.y, 0.01);
    float sonarFadeDuration = max(_SonarRevealWaveParams.z, 0.05);
    float sonarPulseIntensity = saturate(_SonarRevealWaveParams.w);
    float arrivalTime = _SonarRevealWaveParams.x + distanceToOrigin / sonarWaveSpeed;
    float timeSinceArrival = _Time.y - arrivalTime;
    float decay = 1.0 - saturate(max(timeSinceArrival, 0.0) / sonarFadeDuration);
    float waveRadius = max(0.0, _SonarWaveFront);
    float waveBandWidth = lerp(6.0, 2.0, sonarPulseIntensity);
    float waveBand = 1.0 - saturate(abs(distanceToOrigin - waveRadius) / max(waveBandWidth, 0.25));
    float active = step(_Time.y, _SonarRevealExpireTime);
    return active * max(decay, waveBand * sonarPulseIntensity) * sonarPulseIntensity;
}

float HectonCoreLitSampleCaveVoxelSignedDistance(float3 positionWS)
{
    if (_HectonCaveVoxelActive <= 0.5)
        return _HectonCaveVoxelHalfExtents.w;

    float3 halfExtents = max(_HectonCaveVoxelHalfExtents.xyz, float3(0.001, 0.001, 0.001));
    float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
    float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
        return _HectonCaveVoxelHalfExtents.w;

    float encoded = SAMPLE_TEXTURE3D_LOD(_HectonCaveVoxelSdfTex, sampler_HectonCaveVoxelSdfTex, sampleUv, 0).r;
    return lerp(-_HectonCaveVoxelHalfExtents.w, _HectonCaveVoxelHalfExtents.w, encoded);
}

float HectonCoreLitEvaluateCaveAmbientFactor(float3 positionWS, float3 normalWS)
{
    if (_HectonCaveVoxelActive <= 0.5)
        return 1.0;

    float signedDistance = HectonCoreLitSampleCaveVoxelSignedDistance(positionWS + normalWS * 0.03);
    float fadeStart = max(_HectonCaveVoxelAoParams.x, 0.001);
    float fadeEnd = max(_HectonCaveVoxelAoParams.y, fadeStart + 0.001);
    float intensity = saturate(_HectonCaveVoxelAoParams.z);
    float floorValue = saturate(_HectonCaveVoxelAoParams.w);
    float wallProximity = 1.0 - smoothstep(fadeStart, fadeEnd, signedDistance);
    float attenuation = saturate(wallProximity * intensity);
    return lerp(1.0, floorValue, attenuation);
}

float HectonCoreLitEvaluateMainLightContactShadow(float3 surfacePositionWS, float3 normalWS)
{
    if (_HectonContactShadowStrength <= 0.0001 || _HectonContactShadowMaxDistance <= 0.0001)
        return 1.0;

    Light mainLight = GetMainLight();
    float3 lightDirectionWS = HectonCoreLitSafeNormalize(mainLight.direction);
    float noL = saturate(dot(normalWS, lightDirectionWS));
    if (noL <= 0.0001)
        return 1.0;

    float3 biasedSurfacePositionWS = surfacePositionWS + normalWS * max(_HectonContactShadowBias, 0.001);
    int stepCount = clamp((int)round(_HectonContactShadowSteps), 1, 8);
    float shadowOcclusion = 0.0;

    [loop]
    for (int stepIndex = 0; stepIndex < 8; stepIndex++)
    {
        if (stepIndex >= stepCount)
            break;

        float stepT = (stepIndex + 1.0) * rcp((float)stepCount + 1.0);
        float3 raySampleWS = biasedSurfacePositionWS + lightDirectionWS * (_HectonContactShadowMaxDistance * stepT);
        float4 raySampleCS = TransformWorldToHClip(raySampleWS);
        if (raySampleCS.w <= 0.0)
            continue;

        float2 raySampleUV = raySampleCS.xy * rcp(raySampleCS.w) * 0.5 + 0.5;
        if (raySampleUV.x <= 0.0 || raySampleUV.x >= 1.0 || raySampleUV.y <= 0.0 || raySampleUV.y >= 1.0)
            continue;

        float sampledRawDepth = SampleSceneDepth(raySampleUV);
    #if UNITY_REVERSED_Z
        float sampledDepthValid = step(0.0001, sampledRawDepth);
    #else
        float sampledDepthValid = step(sampledRawDepth, 0.9999);
    #endif
        if (sampledDepthValid <= 0.5)
            continue;

        float3 sampledScenePositionWS = ComputeWorldSpacePosition(raySampleUV, sampledRawDepth, UNITY_MATRIX_I_VP);
        float sceneEyeDistance = distance(_WorldSpaceCameraPos, sampledScenePositionWS);
        float rayEyeDistance = distance(_WorldSpaceCameraPos, raySampleWS);
        float depthDiscontinuity = rayEyeDistance - sceneEyeDistance;
        float occluded = step(max(_HectonContactShadowBias * 0.5, 0.001), depthDiscontinuity);
        shadowOcclusion = max(shadowOcclusion, occluded * noL);
    }

    return lerp(1.0, 0.2, saturate(shadowOcclusion * _HectonContactShadowStrength));
}

float3 HectonCoreLitSampleBiolumVolumeRadiance(float3 positionWS)
{
    if (_HectonBiolumVolumeActive <= 0.5)
        return 0.0;

    float3 halfExtents = max(_HectonBiolumVolumeHalfExtents.xyz, float3(0.001, 0.001, 0.001));
    float3 localPosition = mul(_HectonBiolumVolumeWorldToLocal, float4(positionWS, 1.0)).xyz;
    float3 sampleUv = localPosition / (halfExtents * 2.0) + 0.5;
    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
        return 0.0;

    float4 volumeSample = SAMPLE_TEXTURE3D_LOD(_HectonBiolumVolumeTex, sampler_HectonBiolumVolumeTex, sampleUv, 0);
    float sonarReactiveBoost = HectonCoreLitEvaluateSonarReactiveBiolumBoost(positionWS);
    return volumeSample.rgb * max(_HectonBiolumVolumeParams.x, 0.0) * (1.0 + sonarReactiveBoost * 2.5);
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

    float3 lightVector = _HectonFlashlightPositionWS.xyz - surfacePositionWS;
    float lightDistance = length(lightVector);
    if (lightDistance <= 0.0001)
        return 1.0;

    float shadowFloor = HectonCoreLitResolveFlashlightShadowFloor();
    float shadowBias = max(_HectonFlashlightShadowBias, 0.001);
    float rayLength = max(lightDistance - shadowBias, 0.0);
    if (rayLength <= 0.0001)
        return 1.0;

    float3 rayDirectionWS = lightVector / lightDistance;
    float3 rayOriginWS = surfacePositionWS + HectonCoreLitSafeNormalize(normalWS) * shadowBias;
    const int maxVoxelShadowSteps = HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS;
    int stepCount = min(maxVoxelShadowSteps, clamp((int)round(_HectonFlashlightShadowSteps), 1, maxVoxelShadowSteps));
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

float HectonCoreLitResolveFlashlightAdditionalShadow(uint lightLoopIndex, float3 positionWS, float3 normalWS, float defaultShadowAttenuation)
{
    if (_HectonFlashlightActive <= 0.5 || _HectonFlashlightVoxelActive <= 0.5)
        return defaultShadowAttenuation;

    float3 additionalLightPositionWS;
    float3 additionalSpotDirectionWS;
    if (!HectonCoreLitTryResolveAdditionalLight(lightLoopIndex, additionalLightPositionWS, additionalSpotDirectionWS))
        return defaultShadowAttenuation;

    float3 positionDelta = additionalLightPositionWS - _HectonFlashlightPositionWS.xyz;
    if (dot(positionDelta, positionDelta) > 0.0625)
        return defaultShadowAttenuation;

    float directionMatch = dot(
        HectonCoreLitSafeNormalize(additionalSpotDirectionWS),
        HectonCoreLitSafeNormalize(_HectonFlashlightDirectionWS.xyz));
    if (directionMatch < 0.98)
        return defaultShadowAttenuation;

    return min(defaultShadowAttenuation, HectonCoreLitEvaluateFlashlightShadow(positionWS, normalWS));
}

#endif
