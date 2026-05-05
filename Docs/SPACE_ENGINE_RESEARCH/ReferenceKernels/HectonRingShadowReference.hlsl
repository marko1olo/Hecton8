#ifndef HECTON_RING_SHADOW_REFERENCE_INCLUDED
#define HECTON_RING_SHADOW_REFERENCE_INCLUDED

// Status: REFERENCE
// Source: clean-room ring plane shadow translation from exposed SpaceEngine
// ring/eclipses parameters and cache symbols. Not SpaceEngine source.

TEXTURE2D(_RingOpacityTex);
SAMPLER(sampler_RingOpacityTex);

float H8RingShadowTransmittance(
    float3 worldPos,
    float3 lightDir,
    float3 ringCenter,
    float3 ringNormal,
    float innerRadius,
    float outerRadius,
    float opacity,
    float density,
    float edgeSoftness)
{
    float denom = dot(lightDir, ringNormal);

    if (abs(denom) < 1e-5)
    {
        return 1.0;
    }

    float t = dot(ringCenter - worldPos, ringNormal) / denom;

    if (t <= 0.0)
    {
        return 1.0;
    }

    float3 hit = worldPos + lightDir * t;
    float radial = length(hit - ringCenter);

    float inner = smoothstep(innerRadius - edgeSoftness, innerRadius + edgeSoftness, radial);
    float outer = 1.0 - smoothstep(outerRadius - edgeSoftness, outerRadius + edgeSoftness, radial);
    float ringMask = saturate(inner * outer);

    float opticalDepth = max(0.0, opacity * density * ringMask);
    return exp(-opticalDepth);
}

float H8TexturedRingShadowTransmittance(
    float3 worldPos,
    float3 lightDir,
    float3 ringCenter,
    float3 ringNormal,
    float innerRadius,
    float outerRadius,
    float opacity,
    float density,
    float edgeSoftness)
{
    float denom = dot(lightDir, ringNormal);

    if (abs(denom) < 1e-5)
    {
        return 1.0;
    }

    float t = dot(ringCenter - worldPos, ringNormal) / denom;

    if (t <= 0.0)
    {
        return 1.0;
    }

    float3 hit = worldPos + lightDir * t;
    float radial = length(hit - ringCenter);
    float u = saturate((radial - innerRadius) / max(outerRadius - innerRadius, 1e-3));
    float textureMask = SAMPLE_TEXTURE2D(_RingOpacityTex, sampler_RingOpacityTex, float2(u, 0.5)).r;

    float inner = smoothstep(innerRadius - edgeSoftness, innerRadius + edgeSoftness, radial);
    float outer = 1.0 - smoothstep(outerRadius - edgeSoftness, outerRadius + edgeSoftness, radial);
    float opticalDepth = max(0.0, opacity * density * saturate(inner * outer) * textureMask);
    return exp(-opticalDepth);
}

#endif
