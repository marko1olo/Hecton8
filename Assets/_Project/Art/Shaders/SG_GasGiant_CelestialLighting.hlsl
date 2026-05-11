// -----------------------------------------------------------------
// SG_GasGiant_CelestialLighting.hlsl
// Shader Graph custom lighting for gas giants.
// Connected through a Custom Function node.
// -----------------------------------------------------------------

#ifndef SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED
#define SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED

float GasGiantApproxMagnitude3D(float3 value)
{
    float3 a = abs(value);
    float maxAxis = max(max(a.x, a.y), a.z);
    float minAxis = min(min(a.x, a.y), a.z);
    float midAxis = a.x + a.y + a.z - maxAxis - minAxis;
    return max(maxAxis + midAxis * 0.375 + minAxis * 0.1875, 0.0001);
}

float3 GasGiantNormalizeApprox3D(float3 value)
{
    return value * rcp(GasGiantApproxMagnitude3D(value));
}

float GasGiantFastPower01(float value, float exponent)
{
    float v = saturate(value);
    float v2 = v * v;
    float v4 = v2 * v2;
    float v8 = v4 * v4;
    float low = lerp(v, v2, saturate(exponent - 1.0));
    float high = lerp(v2, v8, saturate((exponent - 2.0) * 0.16666667));
    return lerp(low, high, step(2.0, exponent));
}

// -------------------------------------------------------
// MAIN CUSTOM LIGHTING
// -------------------------------------------------------
// Input: world normal, sun direction, albedo, backlight intensity, phase.
// Output: final color.

void GasGiantLighting_float(
    float3 WorldNormal,
    float3 SunDirection,
    float3 Albedo,
    float  BacklitIntensity,
    float3 ViewDirection,
    out float3 FinalColor,
    out float  TerminatorMask,
    out float  FresnelGlow
)
{
    // Cinematic approximate unit vectors; avoids full normalization in fragment work.
    float3 N = GasGiantNormalizeApprox3D(WorldNormal);
    float3 L = GasGiantNormalizeApprox3D(-SunDirection);
    float3 V = GasGiantNormalizeApprox3D(ViewDirection);

    // Base lighting with soft terminator.
    float NdotL = dot(N, L);

    // Widen the transition zone instead of clamping NdotL.
    float terminatorWidth = 0.15;
    float invTerminatorSpan = rcp(2.0 * terminatorWidth + 0.001);
    float softNdotL = saturate((NdotL + terminatorWidth) * invTerminatorSpan);

    // Smoothstep softens the terminator further.
    softNdotL = smoothstep(0.0, 1.0, softNdotL);

    // Rayleigh-like orange rim on the day/night boundary.
    float terminatorZone = 1.0 - abs(NdotL) * invTerminatorSpan;
    terminatorZone = saturate(terminatorZone);
    terminatorZone = terminatorZone * terminatorZone;

    float3 rayleighColor = float3(1.0, 0.5, 0.15);
    float3 rayleighContribution = rayleighColor * terminatorZone * 0.4;

    TerminatorMask = terminatorZone;

    // Lift the shadow side with scattered stellar background instead of pure black.
    float shadowSide = saturate(-NdotL);
    float3 backlitColor = float3(0.03, 0.04, 0.08);
    float3 backlit = backlitColor * shadowSide * BacklitIntensity;

    // Tight Fresnel rim glow.
    float fresnel = 1.0 - saturate(dot(N, V));
    fresnel = fresnel * fresnel * fresnel;

    // Fresnel glow is active mainly in backlight.
    float backlit_facing = saturate(dot(-V, L));
    FresnelGlow = fresnel * backlit_facing;

    float3 daylight = Albedo * softNdotL;
    float3 terminator = rayleighContribution * Albedo;
    float3 rim = float3(0.6, 0.7, 1.0) * FresnelGlow * 0.5;

    FinalColor = daylight + terminator + backlit + rim;
}

// -------------------------------------------------------
// DIFFERENTIAL ROTATION HELPER
// -------------------------------------------------------
// Computes UV offset from latitude and time.

void DifferentialRotation_float(
    float2 UV,
    float  Time,
    float  EquatorialSpeed,
    float  PolarMultiplier,
    out float2 RotatedUV
)
{
    // Latitude mask: 0 at equator, 1 at poles.
    float latitude = abs(UV.y - 0.5) * 2.0;
    float latitudeMask = 1.0 - latitude;

    // Equator rotates faster than poles.
    float speed = lerp(EquatorialSpeed * PolarMultiplier, EquatorialSpeed, latitudeMask);

    // Cheap cos-squared latitude approximation.
    float cosLat = latitudeMask;
    speed *= cosLat;

    RotatedUV = float2(UV.x + Time * speed, UV.y);
}

// -------------------------------------------------------
// MULTI-LAYER ATMOSPHERE FRESNEL
// -------------------------------------------------------

void AtmosphereFresnel_float(
    float3 WorldNormal,
    float3 ViewDirection,
    float3 SunDirection,
    float3 AtmosphereColorInner,
    float3 AtmosphereColorOuter,
    float  InnerPower,
    float  OuterPower,
    out float3 AtmosphereColor,
    out float  AtmosphereAlpha
)
{
    float3 N = GasGiantNormalizeApprox3D(WorldNormal);
    float3 V = GasGiantNormalizeApprox3D(ViewDirection);
    float3 L = GasGiantNormalizeApprox3D(-SunDirection);

    float NdotV = saturate(dot(N, V));
    float fresnel = 1.0 - NdotV;

    // Two Fresnel layers.
    float innerFresnel = GasGiantFastPower01(fresnel, InnerPower);
    float outerFresnel = GasGiantFastPower01(fresnel, OuterPower);

    // Backlight flash: outer layer brightens when backlit.
    float backFacing = saturate(dot(-V, L));
    float backlitBoost = 1.0 + backFacing * 3.0;

    float3 inner = AtmosphereColorInner * innerFresnel;
    float3 outer = AtmosphereColorOuter * outerFresnel * backlitBoost;

    AtmosphereColor = inner + outer;
    AtmosphereAlpha = saturate(innerFresnel + outerFresnel * backlitBoost);
}

#endif // SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED
