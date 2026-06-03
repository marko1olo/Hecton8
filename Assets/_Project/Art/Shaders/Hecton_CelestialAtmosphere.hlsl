#ifndef HECTON_CELESTIAL_ATMOSPHERE_INCLUDED
#define HECTON_CELESTIAL_ATMOSPHERE_INCLUDED

TEXTURE2D(_HectonAtmosphereScatteringLUT);
SAMPLER(sampler_HectonAtmosphereScatteringLUT);
#define HECTON_CELESTIAL_ATMOSPHERE_LUT_SAMPLE_COUNT 16
float4 _CelestialAtmosphereLUTSamples[HECTON_CELESTIAL_ATMOSPHERE_LUT_SAMPLE_COUNT];
float _AtmosphereExposure;
float _CelestialAtmosphereBlendPower;
float _CelestialAtmosphereLUTReady;
float _HectonAtmosphereScatteringLUTReady;
float _CelestialHorizonDensity;
float _CelestialZenithTransparency;

static const float HECTON_INV_HALF_PI = 0.63661977236758134;

float3 HectonCelestialSafeNormalize(float3 value, float3 fallback)
{
    float lengthSq = dot(value, value);
    return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
}

float HectonCelestialFastMaskPower(float value, float power)
{
    float v2 = value * value;
    float v4 = v2 * v2;
    float low = lerp(value, v2, saturate(power - 1.0));
    float high = lerp(v2, v4, saturate((power - 2.0) * 0.5));
    return power < 2.0 ? low : high;
}

float HectonCelestialElevation01(float3 viewRay)
{
    float3 normalizedRay = HectonCelestialSafeNormalize(viewRay, float3(0.0, 1.0, 0.0));
    float clampedY = saturate(normalizedRay.y);
    float elevation01 = saturate(clampedY * (1.18 - 0.18 * clampedY));
    return HectonCelestialFastMaskPower(saturate(elevation01), max(_CelestialAtmosphereBlendPower, 0.01));
}

float4 BuildFallbackHectonCelestialAtmosphere(
    float elevation01,
    float3 skyHorizon,
    float3 skyZenith)
{
    float3 fallbackSky = lerp(skyHorizon, skyZenith, elevation01);
    float horizonTransmittance = saturate(1.0 - saturate(_CelestialHorizonDensity) * 0.35);
    float zenithTransmittance = saturate(lerp(0.82, 1.0, _CelestialZenithTransparency));
    float fallbackTransmittance = lerp(horizonTransmittance, zenithTransmittance, elevation01);
    return float4(max(fallbackSky, 0.0), fallbackTransmittance);
}

float4 SampleHectonCelestialAtmosphere(
    float3 viewRay,
    float3 skyHorizon,
    float3 skyZenith,
    float3 sunDirection)
{
    float elevation01 = HectonCelestialElevation01(viewRay);
    float4 authoredSample = BuildFallbackHectonCelestialAtmosphere(
        elevation01,
        skyHorizon,
        skyZenith);

    if (_CelestialAtmosphereLUTReady >= 0.5)
    {
        float samplePosition = saturate(elevation01) * (HECTON_CELESTIAL_ATMOSPHERE_LUT_SAMPLE_COUNT - 1);
        float lowerSample = floor(samplePosition);
        uint lowerIndex = (uint)min(lowerSample, (float)(HECTON_CELESTIAL_ATMOSPHERE_LUT_SAMPLE_COUNT - 1));
        uint upperIndex = min(lowerIndex + 1u, (uint)(HECTON_CELESTIAL_ATMOSPHERE_LUT_SAMPLE_COUNT - 1));
        authoredSample = lerp(
            _CelestialAtmosphereLUTSamples[lowerIndex],
            _CelestialAtmosphereLUTSamples[upperIndex],
            frac(samplePosition));
    }

    if (_HectonAtmosphereScatteringLUTReady < 0.5)
        return authoredSample;

    float3 normalizedSun = HectonCelestialSafeNormalize(sunDirection, float3(0.0, 1.0, 0.0));
    float3 normalizedRay = HectonCelestialSafeNormalize(viewRay, float3(0.0, 1.0, 0.0));
    float sunView01 = saturate(dot(normalizedRay, normalizedSun) * 0.5 + 0.5);
    float4 physicalSample = SAMPLE_TEXTURE2D(
        _HectonAtmosphereScatteringLUT,
        sampler_HectonAtmosphereScatteringLUT,
        float2(sunView01, elevation01));

    physicalSample.rgb *= max(authoredSample.rgb, 0.0001);
    physicalSample.a = saturate(physicalSample.a * authoredSample.a);
    return physicalSample;
}

float ResolveHectonCelestialTransmittance(float lutTransmittance, float weight)
{
    return lerp(1.0, saturate(lutTransmittance), saturate(weight));
}

float3 ResolveHectonCelestialInscattering(float3 lutInscattering, float weight)
{
    float exposure = max(_AtmosphereExposure, 0.0);
    return max(lutInscattering, 0.0) * exposure * max(weight, 0.0);
}

float3 ApplyHectonCelestialAtmosphere(
    float3 bodyColor,
    float4 atmosphereSample,
    float transmittanceWeight,
    float inscatteringWeight)
{
    float transmittance = ResolveHectonCelestialTransmittance(
        atmosphereSample.a,
        transmittanceWeight);
    float3 inscattering = ResolveHectonCelestialInscattering(
        atmosphereSample.rgb,
        inscatteringWeight);
    return bodyColor * transmittance + inscattering;
}

#endif
