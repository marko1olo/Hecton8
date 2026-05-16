#ifndef HECTON_WATER_EXTINCTION_INCLUDED
#define HECTON_WATER_EXTINCTION_INCLUDED

TEXTURE2D(_ExtinctionLUT);

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

float H8WaterExtinctionActive()
{
    return step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTParams.w, 0.0)) *
        step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTRuntime.w, 1.0));
}

float H8WaterExtinctionResolveTurbidity01(half turbidityMultiplier)
{
    float turbidity = max(0.0, H8WaterExtinctionFinite((float)turbidityMultiplier, 1.0));
    turbidity += max(0.0, H8WaterExtinctionFinite(_ExtinctionLUTWeatherParams.x, 0.0));
    float turbidityMax = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.y, 2.5), 0.001);
    return saturate(turbidity * rcp(turbidityMax));
}

float H8WaterExtinctionDepth01FromWorld(float3 positionWS)
{
    float3 safePositionWS = H8WaterExtinctionFinite3(positionWS, float3(0.0, 0.0, 0.0));
    float surfaceY = H8WaterExtinctionFinite(_ExtinctionLUTRuntime.x, 0.0);
    float depthMeters = max(0.0, surfaceY - safePositionWS.y);
    float maxDepthMeters = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.x, 1500.0), 0.001);
    return saturate(depthMeters * rcp(maxDepthMeters));
}

float H8WaterExtinctionDepth01FromMeters(float depthMeters)
{
    float safeDepthMeters = max(0.0, H8WaterExtinctionFinite(depthMeters, 0.0));
    float maxDepthMeters = max(H8WaterExtinctionFinite(_ExtinctionLUTParams.x, 1500.0), 0.001);
    return saturate(safeDepthMeters * rcp(maxDepthMeters));
}

half H8WaterExtinctionSamplePacked(float depth01, float turbidity01, half wavelength01)
{
    float active = H8WaterExtinctionActive();
    if (active <= 0.0)
        return 1.0h;

    uint depthIndex = (uint)(saturate(depth01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint turbidityIndex = (uint)(saturate(turbidity01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint wavelengthIndex = (uint)(saturate((float)wavelength01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint flatIndex = ((depthIndex * H8_WATER_EXTINCTION_AXIS) + turbidityIndex) * H8_WATER_EXTINCTION_AXIS + wavelengthIndex;
    uint2 texel = uint2(flatIndex & (H8_WATER_EXTINCTION_PACK_WIDTH - 1u), flatIndex >> 12);
    half extinction = LOAD_TEXTURE2D(_ExtinctionLUT, int2(texel)).r;
    extinction = isfinite(extinction) ? saturate(extinction) : 1.0h;
    return lerp(1.0h, extinction, (half)saturate(H8WaterExtinctionFinite(_ExtinctionLUTParams.z, 1.0)) * (half)active);
}

half3 H8WaterExtinctionSampleRgb(float depth01, half turbidityMultiplier)
{
    float turbidity01 = H8WaterExtinctionResolveTurbidity01(turbidityMultiplier);
    const half greenWavelength01 = 0.260869565h; // (530nm - 470nm) / (700nm - 470nm)
    return half3(
        H8WaterExtinctionSamplePacked(depth01, turbidity01, 1.0h),
        H8WaterExtinctionSamplePacked(depth01, turbidity01, greenWavelength01),
        H8WaterExtinctionSamplePacked(depth01, turbidity01, 0.0h));
}

half3 H8WaterExtinctionSampleRgbByWorld(float3 positionWS, half turbidityMultiplier)
{
    return H8WaterExtinctionSampleRgb(H8WaterExtinctionDepth01FromWorld(positionWS), turbidityMultiplier);
}

half3 H8WaterExtinctionSampleRgbByDepthMeters(float depthMeters, half turbidityMultiplier)
{
    return H8WaterExtinctionSampleRgb(H8WaterExtinctionDepth01FromMeters(depthMeters), turbidityMultiplier);
}

float H8WaterExtinctionInterleavedGradientNoise(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

half3 H8WaterExtinctionApplyFogTint(half3 fogColor, half3 extinctionColor, half blend01)
{
    half3 tintedFog = fogColor * max(extinctionColor, half3(0.04h, 0.08h, 0.16h));
    return lerp(fogColor, max(tintedFog, half3(0.0015h, 0.0023h, 0.0031h)), saturate(blend01));
}

#endif
