#ifndef HECTON_SNELL_REFRACTION_CORE_INCLUDED
#define HECTON_SNELL_REFRACTION_CORE_INCLUDED

float4 HectonSanitizeIorLut(float4 rawIorLut)
{
    float airIor = max(1.0001, rawIorLut.x);
    float waterIor = max(airIor, rawIorLut.y);
    float denseWaterIor = max(waterIor, rawIorLut.z);
    float glassIor = max(waterIor, rawIorLut.w);
    return float4(airIor, waterIor, denseWaterIor, glassIor);
}

float HectonFinite01(float value)
{
    return isfinite(value) ? saturate(value) : 0.0;
}

float HectonSnellBend01(float nDotV, float waterDensity01, float4 rawIorLut)
{
    float4 iorLut = HectonSanitizeIorLut(rawIorLut);
    float safeNdotV = saturate(nDotV);
    float density01 = HectonFinite01(waterDensity01);
    float mediumIor = lerp(iorLut.y, iorLut.z, density01);
    float eta = iorLut.x * rcp(max(iorLut.w, 1.0001));
    float sin2Incident = saturate(1.0 - safeNdotV * safeNdotV);
    float sin2Transmitted = saturate(eta * eta * sin2Incident);
    float cosTransmittedApprox = saturate(1.0 - sin2Transmitted * (0.5 + sin2Transmitted * 0.125));
    float glassBend = saturate(1.0 - cosTransmittedApprox);
    float exitContrast = abs(iorLut.w - mediumIor) * rcp(max(iorLut.w, 1.0001));
    return saturate(glassBend * (0.35 + exitContrast * 1.35));
}

float HectonDepthBehindMask(float linearSceneDepth, float linearSurfaceDepth, float sceneDepthValid, float softnessMeters)
{
    float safeSoftness = max(0.001, softnessMeters);
    float behindMask = smoothstep(-safeSoftness, safeSoftness, linearSceneDepth - linearSurfaceDepth);
    return lerp(1.0, behindMask, HectonFinite01(sceneDepthValid));
}

float HectonInverseDirtMask(float dirt01)
{
    return saturate(1.0 - HectonFinite01(dirt01));
}

float2 HectonClampUvOffset(float2 offset, float maxComponentAbs)
{
    float safeMax = min(max(abs(maxComponentAbs), 0.0), 0.1);
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
    float amplitude = max(0.0, strength) * bend * HectonFinite01(depthMask) * HectonFinite01(inverseDirtMask);
    return HectonClampUvOffset(safeNormal * amplitude, 0.1);
}

#endif
