#ifndef HECTON_WATER_EXTINCTION_INCLUDED
#define HECTON_WATER_EXTINCTION_INCLUDED

TEXTURE2D(_ExtinctionLUT);

float4 _ExtinctionLUTParams;        // x=max depth m, y=max turbidity, z=strength, w=active
float4 _ExtinctionLUTRuntime;       // x=sea surface y, y=turbidity multiplier, z=post fog blend, w=underwater active
float4 _ExtinctionLUTWeatherParams; // x=weather turbidity shift, y=weather intensity, z/w=reserved
float4 _ExtinctionLUT_TexelSize;    // xy=texel size, zw=texture dimensions

CBUFFER_START(_GlobalWaterOptics)
    float4 _H8WaterOpticsAbsorptionCoefficientsRGB;         // xyz absorption, w extinction multiplier
    float4 _H8WaterOpticsScatteringCoefficientsRGB;         // xyz scattering, w anisotropy
    float4 _H8WaterOpticsDirectionalLightColorAndIntensity; // xyz light color, w intensity
    float4 _H8WaterOpticsQualityAndDepthLimits;             // x GlobalQualityWeight, y camera-local surface Y, z max travel, w active
CBUFFER_END

#define H8_WATER_EXTINCTION_AXIS 256u
#define H8_WATER_EXTINCTION_AXIS_MAX 255.0
#define H8_WATER_EXTINCTION_CHANNELS 3u
#define H8_WATER_EXTINCTION_PACK_WIDTH 768u
#define H8_WATER_EXTINCTION_PACK_HEIGHT 256u

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

float H8WaterOpticsActive()
{
    return step(0.001, H8WaterExtinctionFinite(_H8WaterOpticsQualityAndDepthLimits.w, 0.0));
}

float H8WaterOpticsQualityWeight()
{
    return saturate(H8WaterExtinctionFinite(_H8WaterOpticsQualityAndDepthLimits.x, 1.0));
}

float3 H8WaterOpticsAbsorptionCoefficients()
{
    float multiplier = max(0.0, H8WaterExtinctionFinite(_H8WaterOpticsAbsorptionCoefficientsRGB.w, 1.0));
    return max(H8WaterExtinctionFinite3(_H8WaterOpticsAbsorptionCoefficientsRGB.xyz, float3(0.42, 0.105, 0.028)), float3(0.0, 0.0, 0.0)) * multiplier;
}

float3 H8WaterOpticsScatteringCoefficients()
{
    return max(H8WaterExtinctionFinite3(_H8WaterOpticsScatteringCoefficientsRGB.xyz, float3(0.035, 0.09, 0.16)), float3(0.0, 0.0, 0.0));
}

float3 H8WaterOpticsExtinctionCoefficients()
{
    return H8WaterOpticsAbsorptionCoefficients() + H8WaterOpticsScatteringCoefficients();
}

float H8WaterOpticsSmooth01(float value)
{
    value = saturate(value);
    return value * value * (3.0 - 2.0 * value);
}

float H8WaterOpticsSpectralAdmissionWeight(float quality)
{
    return H8WaterOpticsSmooth01(saturate((quality - 0.28) * 1.3888889));
}

float3 H8WaterOpticsSafeNormalize(float3 value, float3 fallbackValue)
{
    float lenSq = dot(value, value);
    return isfinite(lenSq) && lenSq > 0.000001 ? value * rsqrt(lenSq) : fallbackValue;
}

float H8WaterOpticsLocalSurfaceY()
{
    return H8WaterExtinctionFinite(_H8WaterOpticsQualityAndDepthLimits.y, 0.0);
}

float H8WaterOpticsDepthMetersAtWorld(float3 positionWS)
{
    float3 cameraWS = GetCameraPositionWS();
    float3 localPosition = H8WaterExtinctionFinite3(positionWS - cameraWS, float3(0.0, 0.0, 0.0));
    return max(0.0, H8WaterOpticsLocalSurfaceY() - localPosition.y);
}

float H8WaterOpticsTravelMeters(float3 positionWS, float3 viewDirWS)
{
    float3 cameraWS = GetCameraPositionWS();
    float3 localPosition = H8WaterExtinctionFinite3(positionWS - cameraWS, float3(0.0, 0.0, 0.0));
    float pixelDepth = max(0.0, H8WaterOpticsLocalSurfaceY() - localPosition.y);
    float cameraSegment = length(localPosition);
    float maxTravel = max(1.0, H8WaterExtinctionFinite(_H8WaterOpticsQualityAndDepthLimits.z, 5000.0));
    return min(pixelDepth + cameraSegment, maxTravel);
}

float3 H8WaterOpticsTransmittanceCompressed(float3 extinctionCoefficients, float travelMeters)
{
    float quality = H8WaterOpticsQualityWeight();
    float spectralWeight = H8WaterOpticsSpectralAdmissionWeight(quality);
    float distance = min(max(0.0, H8WaterExtinctionFinite(travelMeters, 0.0)), max(1.0, H8WaterExtinctionFinite(_H8WaterOpticsQualityAndDepthLimits.z, 5000.0)));
    float3 extinction = max(H8WaterExtinctionFinite3(extinctionCoefficients, float3(0.0, 0.0, 0.0)), float3(0.0, 0.0, 0.0));
    float monoExtinction = max(dot(extinction, float3(0.299, 0.587, 0.114)), 0.000001);
    float monoTransmittance = exp2(-distance * monoExtinction * 1.44269504089);
    float3 monoExtinction3 = float3(monoExtinction, monoExtinction, monoExtinction);
    float3 monoTransmittance3 = float3(monoTransmittance, monoTransmittance, monoTransmittance);
    [branch]
    if (spectralWeight <= 0.0001)
        return monoTransmittance3;

    float3 spectralDelta = (extinction - monoExtinction3) * distance;
    float3 spectralApprox = monoTransmittance3 * rcp(max(float3(0.0625, 0.0625, 0.0625), float3(1.0, 1.0, 1.0) + spectralDelta * (0.58 + 0.42 * spectralWeight)));
    return saturate(lerp(monoTransmittance3, spectralApprox, spectralWeight));
}

float H8WaterOpticsPhaseSchlick(float cosTheta, float anisotropy)
{
    float g = clamp(H8WaterExtinctionFinite(anisotropy, 0.0), -0.85, 0.85);
    float k = 1.55 * g - 0.55 * g * g * g;
    float denom = max(0.0001, 1.0 - k * clamp(cosTheta, -1.0, 1.0));
    return max(0.0, (1.0 - k * k) * 0.07957747155 * rcp(denom * denom));
}

half3 H8WaterOpticsApplyBeerLambert(
    half3 litColor,
    float3 positionWS,
    half3 viewDirWS,
    half3 lightDirWS,
    half3 lightColor,
    half3 fallbackExtinctionColor)
{
    float active = H8WaterOpticsActive();
    [branch]
    if (active <= 0.0)
        return litColor;

    float depthMeters = H8WaterOpticsDepthMetersAtWorld(positionWS);
    float depthGate = saturate(depthMeters * 4.0);
    [branch]
    if (depthGate <= 0.0)
        return litColor;

    float3 extinction = H8WaterOpticsExtinctionCoefficients();
    float travelMeters = H8WaterOpticsTravelMeters(positionWS, (float3)viewDirWS);
    float3 transmittance = H8WaterOpticsTransmittanceCompressed(extinction, travelMeters);
    float3 scattering = H8WaterOpticsScatteringCoefficients();
    float3 scatterRatio = saturate(scattering * rcp(max(extinction, float3(0.00001, 0.00001, 0.00001))));
    float3 safeView = H8WaterOpticsSafeNormalize((float3)viewDirWS, float3(0.0, 0.0, 1.0));
    float3 safeLight = H8WaterOpticsSafeNormalize((float3)lightDirWS, float3(0.0, 1.0, 0.0));
    float phase = H8WaterOpticsPhaseSchlick(dot(safeLight, safeView), _H8WaterOpticsScatteringCoefficientsRGB.w);
    float quality = H8WaterOpticsQualityWeight();
    float3 opticsLight = max((float3)_H8WaterOpticsDirectionalLightColorAndIntensity.rgb, (float3)lightColor) *
        max(0.0, H8WaterExtinctionFinite(_H8WaterOpticsDirectionalLightColorAndIntensity.w, 0.85));
    float3 biomeTint = max((float3)fallbackExtinctionColor, float3(0.002, 0.003, 0.004));
    float3 inScatter = opticsLight * scatterRatio * (1.0 - transmittance) * phase * lerp(0.45, 2.25, quality);
    inScatter *= lerp(float3(1.0, 1.0, 1.0), biomeTint, saturate(0.24 + 0.28 * quality));
    float3 resolved = (float3)litColor * transmittance + inScatter;
    return (half3)lerp((float3)litColor, resolved, active * depthGate);
}

float H8WaterExtinctionActive()
{
    return step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTParams.w, 0.0)) *
        step(0.5, H8WaterExtinctionFinite(_ExtinctionLUTRuntime.w, 1.0)) *
        step((float)H8_WATER_EXTINCTION_PACK_WIDTH, floor(H8WaterExtinctionFinite(_ExtinctionLUT_TexelSize.z, 0.0) + 0.5)) *
        step((float)H8_WATER_EXTINCTION_PACK_HEIGHT, floor(H8WaterExtinctionFinite(_ExtinctionLUT_TexelSize.w, 0.0) + 0.5));
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
    float quality = lerp(1.0, H8WaterOpticsQualityWeight(), H8WaterOpticsActive());
    float spectralWeight = H8WaterOpticsSpectralAdmissionWeight(quality);
    float monoSigma = max(dot(sigma, float3(0.299, 0.587, 0.114)), 0.000001);
    float monoAnalytical = exp2(-safeDepthMeters * monoSigma * 1.442695);
    float3 analytical = float3(monoAnalytical, monoAnalytical, monoAnalytical);
    [branch]
    if (spectralWeight > 0.0001)
    {
        float3 spectralAnalytical = exp2(-safeDepthMeters * sigma * 1.442695);
        analytical = lerp(analytical, spectralAnalytical, spectralWeight);
    }
    analytical = all(isfinite(analytical)) ? saturate(analytical) : float3(1.0, 1.0, 1.0);
    return (half3)lerp(float3(1.0, 1.0, 1.0), analytical, strength);
}

half3 H8WaterExtinctionAnalyticalRgbByWorld(float3 positionWS, half turbidityMultiplier)
{
    return H8WaterExtinctionAnalyticalRgbByDepthMeters(
        H8WaterExtinctionDepthMetersFromWorld(positionWS),
        turbidityMultiplier);
}

float H8WaterExtinctionLutBlendWeight(float active)
{
    float quality = lerp(1.0, H8WaterOpticsQualityWeight(), H8WaterOpticsActive());
    float qualityWeight = H8WaterOpticsSmooth01(saturate((quality - 0.14) * 1.1627907));
    return saturate(active * H8WaterExtinctionStrength() * qualityWeight);
}

half H8WaterExtinctionSamplePackedChannelRaw(float depth01, float turbidity01, uint channelIndex)
{
    uint depthIndex = (uint)(saturate(depth01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint turbidityIndex = (uint)(saturate(turbidity01) * H8_WATER_EXTINCTION_AXIS_MAX + 0.5);
    uint safeChannel = min(channelIndex, H8_WATER_EXTINCTION_CHANNELS - 1u);
    uint2 texel = uint2(turbidityIndex * H8_WATER_EXTINCTION_CHANNELS + safeChannel, depthIndex);
    half extinction = LOAD_TEXTURE2D(_ExtinctionLUT, int2(texel)).r;
    extinction = isfinite(extinction) ? saturate(extinction) : 1.0h;
    return extinction;
}

half H8WaterExtinctionSamplePackedRaw(float depth01, float turbidity01, half wavelength01)
{
    uint channelIndex = (uint)(saturate(1.0 - (float)wavelength01) * (float)(H8_WATER_EXTINCTION_CHANNELS - 1u) + 0.5);
    return H8WaterExtinctionSamplePackedChannelRaw(depth01, turbidity01, channelIndex);
}

half3 H8WaterExtinctionSampleRgbRaw(float depth01, half turbidityMultiplier)
{
    float turbidity01 = H8WaterExtinctionResolveTurbidity01(turbidityMultiplier);
    return half3(
        H8WaterExtinctionSamplePackedChannelRaw(depth01, turbidity01, 0u),
        H8WaterExtinctionSamplePackedChannelRaw(depth01, turbidity01, 1u),
        H8WaterExtinctionSamplePackedChannelRaw(depth01, turbidity01, 2u));
}

half3 H8WaterExtinctionResolveRgbByWorld(float3 positionWS, half turbidityMultiplier)
{
    half3 analytical = H8WaterExtinctionAnalyticalRgbByWorld(positionWS, turbidityMultiplier);
    float active = H8WaterExtinctionActive();
    float lutBlend = H8WaterExtinctionLutBlendWeight(active);
    [branch]
    if (lutBlend <= 0.0001)
        return analytical;

    half3 sampled = H8WaterExtinctionSampleRgbRaw(H8WaterExtinctionDepth01FromWorld(positionWS), turbidityMultiplier);
    return max(lerp(analytical, sampled, (half)lutBlend), half3(0.0h, 0.0h, 0.0h));
}

half3 H8WaterExtinctionResolveRgbByDepthMeters(float depthMeters, half turbidityMultiplier)
{
    half3 analytical = H8WaterExtinctionAnalyticalRgbByDepthMeters(depthMeters, turbidityMultiplier);
    float active = H8WaterExtinctionActive();
    float lutBlend = H8WaterExtinctionLutBlendWeight(active);
    [branch]
    if (lutBlend <= 0.0001)
        return analytical;

    half3 sampled = H8WaterExtinctionSampleRgbRaw(H8WaterExtinctionDepth01FromMeters(depthMeters), turbidityMultiplier);
    return max(lerp(analytical, sampled, (half)lutBlend), half3(0.0h, 0.0h, 0.0h));
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
