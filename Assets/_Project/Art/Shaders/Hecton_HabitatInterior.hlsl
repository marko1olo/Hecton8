#ifndef HECTON_HABITAT_INTERIOR_INCLUDED
#define HECTON_HABITAT_INTERIOR_INCLUDED

#define HECTON_HABITAT_INTERIOR_PI 3.14159265359
#define HECTON_HABITAT_INTERIOR_MAX_MODULES 64
#define HECTON_HABITAT_INTERIOR_STRESS_EPSILON 0.0015
#define HECTON_HABITAT_INTERIOR_STRESS_EPSILON_HALF 0.0015h

StructuredBuffer<float> _HectonHabitatModuleStressBuffer;
float4 _HectonHabitatModuleStressParams; // x=count, y=max deformation, z=low-tier crease mode, w=peak stress

uint HectonHabitatInteriorModuleCount()
{
    uint stressCount = min((uint)max(_HectonHabitatModuleStressParams.x, 0.0), (uint)HECTON_HABITAT_INTERIOR_MAX_MODULES);
    uint ambienceCount = min((uint)max(_ModuleWaterLevelCount, 0), (uint)HECTON_HABITAT_INTERIOR_MAX_MODULES);
    return min(stressCount, ambienceCount);
}

float3 HectonHabitatInteriorSafeNormalize3(float3 value)
{
    return value * rsqrt(max(dot(value, value), 0.0001));
}

half3 HectonHabitatInteriorSafeNormalizeHalf3(half3 value)
{
    return value * rsqrt(max(dot(value, value), 0.0001h));
}

float HectonHabitatInteriorResolveStress01(float3 positionWS)
{
    if (_HectonHabitatModuleStressParams.w <= HECTON_HABITAT_INTERIOR_STRESS_EPSILON)
        return 0.0;

    if (_HectonHabitatModuleStressParams.z > 0.5)
        return saturate(_HectonHabitatModuleStressParams.w);

    uint count = HectonHabitatInteriorModuleCount();
    if (count == 0u)
        return 0.0;

    uint bestIndex = count;
    float bestDistanceSq = 1.0e20;
    [loop]
    for (uint i = 0u; i < count; i++)
    {
        float4 centerRadius = _HectonModuleAmbienceDataBuffer[i];
        float radius = max(centerRadius.w, 0.001);
        float3 delta = positionWS - centerRadius.xyz;
        float distanceSq = dot(delta, delta);
        if (distanceSq > radius * radius || distanceSq >= bestDistanceSq)
            continue;

        bestDistanceSq = distanceSq;
        bestIndex = i;
    }

    if (bestIndex >= count)
        return 0.0;

    return saturate(_HectonHabitatModuleStressBuffer[bestIndex]);
}

float2 HectonHabitatInteriorPanelUv(float2 uv)
{
    return saturate(frac(abs(uv)));
}

float HectonHabitatInteriorPanelMaskFromUv(float2 panelUv)
{
    float sx = sin(panelUv.x * HECTON_HABITAT_INTERIOR_PI);
    float sy = sin(panelUv.y * HECTON_HABITAT_INTERIOR_PI);
    return saturate(sx * sy);
}

half HectonHabitatInteriorCheapPanelMask(float2 uv)
{
    half2 panelUv = (half2)HectonHabitatInteriorPanelUv(uv);
    half2 triangle = 1.0h - abs(panelUv * 2.0h - 1.0h);
    return saturate(triangle.x * triangle.y);
}

float3 HectonHabitatInteriorApplyPanelBendOS(
    float3 positionOS,
    float3 normalOS,
    float2 uv,
    float stress01,
    out half shadow01,
    out half panelMask01,
    out half2 panelCenteredUv)
{
    shadow01 = 0.0h;
    panelMask01 = 0.0h;
    panelCenteredUv = half2(0.0h, 0.0h);
    float maxDeformation = max(_HectonHabitatModuleStressParams.y, 0.0);
    if (_HectonHabitatModuleStressParams.z > 0.5 || maxDeformation <= 0.00001 || stress01 <= HECTON_HABITAT_INTERIOR_STRESS_EPSILON)
        return positionOS;

    float2 panelUv = HectonHabitatInteriorPanelUv(uv);
    float panelMask = HectonHabitatInteriorPanelMaskFromUv(panelUv);
    if (panelMask <= 0.0001)
        return positionOS;

    panelMask01 = (half)panelMask;
    panelCenteredUv = (half2)(panelUv * 2.0 - 1.0);
    float offsetMeters = panelMask * saturate(stress01) * maxDeformation;
    shadow01 = (half)saturate(panelMask * stress01 * 0.45);
    return positionOS + HectonHabitatInteriorSafeNormalize3(normalOS) * offsetMeters;
}

half3 HectonHabitatInteriorApplyCheapNormalBiasWS(half3 normalWS, float stress01, half panelMask01, half2 panelCenteredUv)
{
    if (_HectonHabitatModuleStressParams.z > 0.5 || stress01 <= HECTON_HABITAT_INTERIOR_STRESS_EPSILON || panelMask01 <= 0.0001h)
        return normalWS;

    half3 baseNormal = HectonHabitatInteriorSafeNormalizeHalf3(normalWS);
    half3 tangentWS = abs(baseNormal.y) < 0.999h
        ? HectonHabitatInteriorSafeNormalizeHalf3(cross(half3(0.0h, 1.0h, 0.0h), baseNormal))
        : half3(1.0h, 0.0h, 0.0h);
    half3 bitangentWS = HectonHabitatInteriorSafeNormalizeHalf3(cross(baseNormal, tangentWS));
    half slopeStrength = (half)(saturate(stress01) * panelMask01 * 0.08);
    return HectonHabitatInteriorSafeNormalizeHalf3(baseNormal - tangentWS * panelCenteredUv.x * slopeStrength - bitangentWS * panelCenteredUv.y * slopeStrength);
}

void HectonHabitatInteriorApplyLowTierCrease(
    half stress01,
    half panelMask,
    half detailMask,
    inout half hullDentShadow,
    inout half3 albedo,
    inout half smoothness)
{
    if (_HectonHabitatModuleStressParams.z <= 0.5 || stress01 <= HECTON_HABITAT_INTERIOR_STRESS_EPSILON_HALF)
        return;

    if (panelMask <= 0.0001h)
        return;

    half crease = saturate(stress01 * panelMask * lerp(0.38h, 1.0h, detailMask));
    hullDentShadow = max(hullDentShadow, crease * 0.32h);
    albedo *= lerp(1.0h, 0.74h, crease);
    smoothness = lerp(smoothness, smoothness * 0.72h, crease);
}

#endif
