#ifndef HECTON_WATER_EXTINCTION_INCLUDED
#define HECTON_WATER_EXTINCTION_INCLUDED

#if !defined(_MATH_LOD_LOW) && !defined(SHADER_API_MOBILE)
    #define H8_WATER_EXTINCTION_LUT_ENABLED 1
    TEXTURE2D(_ExtinctionLUT);
#endif

float4 _ExtinctionLUTParams;        // x=max depth m, y=max turbidity, z=strength, w=active
float4 _ExtinctionLUTRuntime;       // x=sea surface y, y=turbidity multiplier, z=post fog blend, w=underwater active
float4 _ExtinctionLUTWeatherParams; // x=weather turbidity shift, y=weather intensity, z/w=reserved

#define H8_WATER_EXTINCTION_AXIS 256u
#define H8_WATER_EXTINCTION_AXIS_MAX 255.0
#define H8_WATER_EXTINCTION_PACK_WIDTH 4096u

float H8WaterExtinctionFinite(float value, float fallbackValue)
{
    return isfinite(value) ? value : fallbackValue;
}

float3 H8WaterExtinctionFinite3(float3 value, float3 fallbackValue)
{
    return all(isfinite(value)) ? value : fallbackValue;
}

float H8WaterExtinctionSafeRcp(float value)
{
    return rcp(max(abs(H8WaterExtinctionFinite(value, 1.0)), 0.001));
}

float H8WaterExtinctionActive()
{
    return step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTParams.w, 0.0)) *
        step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTRuntime.w, 1.0));
}

float H8WaterExtinctionStrength()
{
    return saturate(H8WaterExtinctionFinite(_ExtinctionLUTParams.z, 0.0));
}

half H8WaterExtinctionFogBlend()
{
    return (half)saturate(H8WaterExtinctionFinite(_ExtinctionLUTRuntime.z, 1.0) * H8WaterExtinctionStrength());
}

float H8WaterExtinctionResolveTurbidity01(half turbidityMultiplier)
{
    float turbidity = max(0.0, H8WaterExtinctionFinite((float)turbidityMultiplier, 1.0));
    turbidity += max(0.0, H8WaterExtinctionFinite(_ExtinctionLUTWeatherParams.x, 0.0));
    float turbidityMax = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.y, 2.5), 0.001);
    return saturate(turbidity * H8WaterExtinctionSafeRcp(turbidityMax));
}

float H8WaterExtinctionDepthMetersFromWorld(float3 positionWS)
{
    float3 safePositionWS = H8WaterExtinctionFinite3(positionWS, float3(0.0, 0.0, 0.0));
    float surfaceY = H8WaterExtinctionFinite(_ExtinctionLUTRuntime.x, 0.0);
    return max(0.0, surfaceY - safePositionWS.y);
}

float H8WaterExtinctionDepth01FromWorld(float3 positionWS)
{
    float depthMeters = H8WaterExtinctionDepthMetersFromWorld(positionWS);
    float maxDepthMeters = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.x, 1500.0), 0.001);
    return saturate(depthMeters * H8WaterExtinctionSafeRcp(maxDepthMeters));
}

float H8WaterExtinctionDepth01FromMeters(float depthMeters)
{
    float safeDepthMeters = max(0.0, H8WaterExtinctionFinite(depthMeters, 0.0));
    float maxDepthMeters = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.x, 1500.0), 0.001);
    return saturate(safeDepthMeters * H8WaterExtinctionSafeRcp(maxDepthMeters));
}

half3 H8WaterExtinctionAnalyticalRgbByDepthMeters(float depthMeters, half turbidityMultiplier)
{
    float safeDepthMeters = min(max(0.0, H8WaterExtinctionFinite(depthMeters, 0.0)), 5000.0);
    float safeTurbidity = max(0.25, H8WaterExtinctionFinite((float)turbidityMultiplier, 1.0));
    safeTurbidity += max(0.0, H8WaterExtinctionFinite(_ExtinctionLUTWeatherParams.x, 0.0));
    float strength = H8WaterExtinctionStrength();
    float3 sigma = float3(0.2303, 0.061, 0.018) * safeTurbidity;
    float3 analytical = exp2(-safeDepthMeters * sigma * 1.442695);
    analytical = all(isfinite(analytical)) ? saturate(analytical) : float3(1.0, 1.0, 1.0);
    return (half3)lerp(float3(1.0, 1.0, 1.0), analytical, strength);
}

half3 H8WaterExtinctionAnalyticalRgbByWorld(float3 positionWS, half turbidityMultiplier)
{
    return H8WaterExtinctionAnalyticalRgbByDepthMeters(
        H8WaterExtinctionDepthMetersFromWorld(positionWS),
        turbidityMultiplier);
}

half H8WaterExtinctionSamplePackedActive(float depth01, float turbidity01, half wavelength01, float active)
{
#if !defined(H8_WATER_EXTINCTION_LUT_ENABLED)
    return 1.0h;
#else
    uint depthIndex = (uint)(saturate(depth01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint turbidityIndex = (uint)(saturate(turbidity01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint wavelengthIndex = (uint)(saturate((float)wavelength01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint flatIndex = ((depthIndex * H8_WATER_EXTINCTION_AXIS) + turbidityIndex) * H8_WATER_EXTINCTION_AXIS + wavelengthIndex;
    uint2 texel = uint2(flatIndex & (H8_WATER_EXTINCTION_PACK_WIDTH - 1u), flatIndex >> 12);
    half extinction = LOAD_TEXTURE2D(_ExtinctionLUT, int2(texel)).r;
    extinction = isfinite(extinction) ? saturate(extinction) : 1.0h;
    return lerp(1.0h, extinction, (half)H8WaterExtinctionStrength() * (half)active);
#endif
}

half3 H8WaterExtinctionSampleRgbActive(float depth01, half turbidityMultiplier, float active)
{
    float turbidity01 = H8WaterExtinctionResolveTurbidity01(turbidityMultiplier);
    const half greenWavelength01 = 0.260869565h; // (530nm - 470nm) / (700nm - 470nm)
    return half3(
        H8WaterExtinctionSamplePackedActive(depth01, turbidity01, 1.0h, active),
        H8WaterExtinctionSamplePackedActive(depth01, turbidity01, greenWavelength01, active),
        H8WaterExtinctionSamplePackedActive(depth01, turbidity01, 0.0h, active));
}

half3 H8WaterExtinctionResolveRgbByWorld(float3 positionWS, half turbidityMultiplier)
{
#if !defined(H8_WATER_EXTINCTION_LUT_ENABLED)
    return H8WaterExtinctionAnalyticalRgbByWorld(positionWS, turbidityMultiplier);
#else
    float active = H8WaterExtinctionActive();
    [branch]
    if (active <= 0.0)
        return H8WaterExtinctionAnalyticalRgbByWorld(positionWS, turbidityMultiplier);

    return max(H8WaterExtinctionSampleRgbActive(H8WaterExtinctionDepth01FromWorld(positionWS), turbidityMultiplier, active), half3(0.0h, 0.0h, 0.0h));
#endif
}

half3 H8WaterExtinctionResolveRgbByDepthMeters(float depthMeters, half turbidityMultiplier)
{
#if !defined(H8_WATER_EXTINCTION_LUT_ENABLED)
    return H8WaterExtinctionAnalyticalRgbByDepthMeters(depthMeters, turbidityMultiplier);
#else
    float active = H8WaterExtinctionActive();
    [branch]
    if (active <= 0.0)
        return H8WaterExtinctionAnalyticalRgbByDepthMeters(depthMeters, turbidityMultiplier);

    return max(H8WaterExtinctionSampleRgbActive(H8WaterExtinctionDepth01FromMeters(depthMeters), turbidityMultiplier, active), half3(0.0h, 0.0h, 0.0h));
#endif
}

float H8WaterExtinctionInterleavedGradientNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

half3 H8WaterExtinctionApplyFogTint(
    half3 fogColor,
    half3 extinctionColor,
    half blend01,
    half3 extinctionFloor,
    half3 abyssFloor)
{
    half3 safeAbyssFloor = max(abyssFloor, half3(0.0h, 0.0h, 0.0h));
    half3 safeExtinctionFloor = max(extinctionFloor, safeAbyssFloor);
    half3 tintedFog = fogColor * max(extinctionColor, safeExtinctionFloor);
    return lerp(fogColor, max(tintedFog, safeAbyssFloor), saturate(blend01));
}

#endif
