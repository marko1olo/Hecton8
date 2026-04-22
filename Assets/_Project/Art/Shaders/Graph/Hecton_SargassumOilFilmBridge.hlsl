#ifndef HECTON_SARGASSUM_OIL_FILM_BRIDGE_INCLUDED
#define HECTON_SARGASSUM_OIL_FILM_BRIDGE_INCLUDED

TEXTURE2D(_SargassumOilFilmMaskRT);
SAMPLER(sampler_SargassumOilFilmMaskRT);

float4 _SargassumOilFilmMaskWorldRect;
float _SargassumOilFilmMaskActive;

inline float HectonSampleWorldRectMask(float2 worldXZ, float4 worldRect)
{
    float2 uv = (worldXZ - worldRect.xy) * worldRect.zw;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 0.0;

    return SAMPLE_TEXTURE2D_LOD(_SargassumOilFilmMaskRT, sampler_SargassumOilFilmMaskRT, uv, 0).r;
}

void Hecton_SargassumOilFilmBridge_float(
    float3 AbsoluteWorldPosition,
    float TimeSeconds,
    float FresnelTerm,
    float4 OilTint,
    float DensityPower,
    float AlphaScale,
    float IridescenceStrength,
    float IridescenceScale,
    float ChromaticAberration,
    out float Mask,
    out float3 Color,
    out float SmoothnessBias)
{
    float density = _SargassumOilFilmMaskActive > 0.5
        ? HectonSampleWorldRectMask(AbsoluteWorldPosition.xz, _SargassumOilFilmMaskWorldRect)
        : 0.0;
    density = pow(saturate(density), max(0.05, DensityPower));
    Mask = saturate(density * AlphaScale);

    float spectralPhase =
        (AbsoluteWorldPosition.x + AbsoluteWorldPosition.z) * IridescenceScale +
        TimeSeconds * 0.28 +
        density * 3.7;
    float3 chromaPhaseOffset = float3(-ChromaticAberration, 0.0, ChromaticAberration) * (0.65 + FresnelTerm * 1.75);
    float3 spectralShift = 0.5 + 0.5 * cos(float3(0.0, 2.0943951, 4.1887902) + spectralPhase + chromaPhaseOffset);
    float spectralMask = density * density * IridescenceStrength * (1.0 + FresnelTerm * ChromaticAberration);

    Color = lerp(OilTint.rgb, saturate(OilTint.rgb + spectralShift * 0.65), spectralMask);
    SmoothnessBias = saturate(Mask * (0.18 + FresnelTerm * 0.22));
}

#endif
