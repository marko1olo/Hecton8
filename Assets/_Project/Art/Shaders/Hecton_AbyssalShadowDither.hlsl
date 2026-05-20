#ifndef HECTON_ABYSSAL_SHADOW_DITHER_INCLUDED
#define HECTON_ABYSSAL_SHADOW_DITHER_INCLUDED

struct H8AbyssalShadowCullState
{
    uint InstanceHash;
    float DistanceSq;
    uint CullFlags;
    float IlluminationScalar;
    uint4 Pad0;
};

StructuredBuffer<H8AbyssalShadowCullState> _H8AbyssalShadowCullStates;
int _H8AbyssalShadowCullCount;
float _H8AbyssalShadowQuality;

#define H8_ABYSSAL_CAST_SHADOWS 2u
#define H8_ABYSSAL_DITHER_FADE_ACTIVE 8u

float H8AbyssalBayer4x4(uint2 pixel)
{
    uint x = pixel.x & 3u;
    uint y = pixel.y & 3u;
    uint index = x + y * 4u;
    const float thresholds[16] =
    {
        0.03125, 0.53125, 0.15625, 0.65625,
        0.78125, 0.28125, 0.90625, 0.40625,
        0.21875, 0.71875, 0.09375, 0.59375,
        0.96875, 0.46875, 0.84375, 0.34375
    };
    return thresholds[index];
}

void H8AbyssalClipShadow(uint instanceIndex, float4 positionCS)
{
    if (instanceIndex >= (uint)_H8AbyssalShadowCullCount)
        return;

    H8AbyssalShadowCullState state = _H8AbyssalShadowCullStates[instanceIndex];
    if ((state.CullFlags & H8_ABYSSAL_CAST_SHADOWS) == 0u)
        clip(-1.0);
    if ((state.CullFlags & H8_ABYSSAL_DITHER_FADE_ACTIVE) == 0u)
        return;

    float2 ndc = positionCS.xy / max(positionCS.w, 0.0001);
    uint2 pixel = (uint2)abs(floor(ndc * 8192.0));
    float threshold = H8AbyssalBayer4x4(pixel);
    clip(saturate(state.IlluminationScalar) - threshold);
}

#endif
