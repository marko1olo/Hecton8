#ifndef HECTON_SNELL_REFRACTION_CORE_INCLUDED
#define HECTON_SNELL_REFRACTION_CORE_INCLUDED

float4 HectonSanitizeIorLut(float4 rawIorLut)
{
    float rawAirIor = isfinite(rawIorLut.x) ? rawIorLut.x : 1.0003;
    float rawWaterIor = isfinite(rawIorLut.y) ? rawIorLut.y : 1.333;
    float rawDenseWaterIor = isfinite(rawIorLut.z) ? rawIorLut.z : 1.38;
    float rawGlassIor = isfinite(rawIorLut.w) ? rawIorLut.w : 1.46;
    float airIor = max(1.0001, rawAirIor);
    float waterIor = max(airIor, rawWaterIor);
    float denseWaterIor = max(waterIor, rawDenseWaterIor);
    float glassIor = max(waterIor, rawGlassIor);
    return float4(airIor, waterIor, denseWaterIor, glassIor);
}

float HectonFinite01(float value)
{
    return isfinite(value) ? saturate(value) : 0.0;
}

float HectonFiniteValue(float value, float fallback)
{
    return isfinite(value) ? value : fallback;
}

float HectonFiniteNonNegative(float value, float fallback)
{
    return max(0.0, HectonFiniteValue(value, fallback));
}

float2 HectonFinite2(float2 value, float2 fallback)
{
    return all(isfinite(value)) ? value : fallback;
}

float3 HectonFinite3(float3 value, float3 fallback)
{
    return all(isfinite(value)) ? value : fallback;
}

float4 HectonFinite4(float4 value, float4 fallback)
{
    return all(isfinite(value)) ? value : fallback;
}

float HectonInvalidSceneRawDepth()
{
#if UNITY_REVERSED_Z
    return 0.0;
#else
    return 1.0;
#endif
}

float HectonFiniteSceneRawDepth(float rawDepth)
{
    return isfinite(rawDepth) ? saturate(rawDepth) : HectonInvalidSceneRawDepth();
}

float HectonSceneDepthValid01(float rawDepth)
{
    float safeRawDepth = HectonFiniteSceneRawDepth(rawDepth);
#if UNITY_REVERSED_Z
    return step(0.0001, safeRawDepth);
#else
    return step(safeRawDepth, 0.9999);
#endif
}

float HectonSnellSafeRcp(float value)
{
    return rcp(max(abs(isfinite(value) ? value : 1.0), 1.0001));
}

float HectonSnellBend01(float nDotV, float waterDensity01, float4 rawIorLut)
{
    float4 iorLut = HectonSanitizeIorLut(rawIorLut);
    float safeNdotV = HectonFinite01(nDotV);
    float density01 = HectonFinite01(waterDensity01);
    float mediumIor = lerp(iorLut.y, iorLut.z, density01);
    float invGlassIor = HectonSnellSafeRcp(iorLut.w);
    float eta = iorLut.x * invGlassIor;
    float sin2Incident = saturate(1.0 - safeNdotV * safeNdotV);
    float sin2Transmitted = saturate(eta * eta * sin2Incident);
    float cosTransmittedApprox = saturate(1.0 - sin2Transmitted * (0.5 + sin2Transmitted * 0.125));
    float glassBend = saturate(1.0 - cosTransmittedApprox);
    float exitContrast = abs(iorLut.w - mediumIor) * invGlassIor;
    return saturate(glassBend * (0.35 + exitContrast * 1.35));
}

float HectonDepthBehindMask(float linearSceneDepth, float linearSurfaceDepth, float sceneDepthValid, float softnessMeters)
{
    float safeSoftness = isfinite(softnessMeters) ? max(0.001, abs(softnessMeters)) : 0.001;
    float safeSurfaceDepth = isfinite(linearSurfaceDepth) ? linearSurfaceDepth : 0.0;
    float safeSceneDepth = isfinite(linearSceneDepth) ? linearSceneDepth : safeSurfaceDepth + safeSoftness;
    float behindMask = smoothstep(-safeSoftness, safeSoftness, safeSceneDepth - safeSurfaceDepth);
    return lerp(1.0, behindMask, HectonFinite01(sceneDepthValid));
}

float HectonInverseDirtMask(float dirt01)
{
    return saturate(1.0 - HectonFinite01(dirt01));
}

float2 HectonClampUvOffset(float2 offset, float maxComponentAbs)
{
    float safeMax = isfinite(maxComponentAbs) ? min(abs(maxComponentAbs), 0.1) : 0.0;
    float2 safeMax2 = float2(safeMax, safeMax);
    return all(isfinite(offset)) ? clamp(offset, -safeMax2, safeMax2) : float2(0.0, 0.0);
}

float2 HectonSnellUvOffset(
    float2 normalXY,
    float nDotV,
    float waterDensity01,
    float4 iorLut,
    float strength,
    float depthMask,
    float inverseDirtMask)
{
    float2 safeNormal = all(isfinite(normalXY)) ? normalXY : float2(0.0, 0.0);
    float bend = HectonSnellBend01(nDotV, waterDensity01, iorLut);
    float safeStrength = isfinite(strength) ? max(0.0, strength) : 0.0;
    float amplitude = safeStrength * bend * HectonFinite01(depthMask) * HectonFinite01(inverseDirtMask);
    return HectonClampUvOffset(safeNormal * amplitude, 0.1);
}

#endif
