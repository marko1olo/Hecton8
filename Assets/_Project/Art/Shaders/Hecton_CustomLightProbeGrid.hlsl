#ifndef HECTON_CUSTOM_LIGHT_PROBE_GRID_INCLUDED
#define HECTON_CUSTOM_LIGHT_PROBE_GRID_INCLUDED

#define H8_CUSTOM_LIGHT_PROBE_EPS 0.0001
#define H8_CUSTOM_LIGHT_PROBE_MAX_LUMA 32.0

struct H8CustomLightProbeDTO
{
    uint2 SpatialHash64;
    uint PackedGridCoord;
    uint Flags;
    float4 Lane0;
    float4 Lane1;
    float4 Lane2;
    float4 Lane3;
    float4 Lane4;
    float4 Lane5;
    float4 Lane6;
};

StructuredBuffer<H8CustomLightProbeDTO> _H8CustomLightProbeGrid;
float4 _H8InteriorGIProbeParams;      // x=resolution, y=cell meters, z=quality, w=directional weight
float4 _H8InteriorGIProbeOrigin;      // xyz=runtime root, w=published
float4 _H8InteriorGIProbeRootAup;     // xyz=AUP residue, w=root hash
float4 _H8CustomLightProbeGridState;  // x=active count, y=grid version, z=stride, w=buffer index

float H8CustomLightProbeSafeRcp(float value)
{
    return rcp(max(abs(value), H8_CUSTOM_LIGHT_PROBE_EPS));
}

float H8CustomLightProbeSmooth01(float value)
{
    float t = saturate(value);
    return t * t * (3.0 - 2.0 * t);
}

float3 H8CustomLightProbeSafeNormal(float3 value, float3 fallbackValue)
{
    float finiteMask = all(isfinite(value)) ? 1.0 : 0.0;
    float3 safeValue = finiteMask > 0.5 ? value : fallbackValue;
    float lenSq = max(dot(safeValue, safeValue), H8_CUSTOM_LIGHT_PROBE_EPS);
    return lerp(fallbackValue, safeValue * rsqrt(lenSq), finiteMask);
}

uint H8CustomLightProbeIndex(uint3 coord, uint resolution)
{
    return coord.x + coord.y * resolution + coord.z * resolution * resolution;
}

uint3 H8CustomLightProbeClampCoord(int3 coord, uint resolution)
{
    int maxCoord = max((int)resolution - 1, 0);
    return (uint3)clamp(coord, int3(0, 0, 0), int3(maxCoord, maxCoord, maxCoord));
}

float3 H8CustomLightProbeEvaluate(H8CustomLightProbeDTO probe, float3 directionWS, float l1Weight, float l2Weight)
{
    float3 d = H8CustomLightProbeSafeNormal(directionWS, float3(0.0, 0.0, 1.0));
    float xy = d.x * d.y;
    float yz = d.y * d.z;
    float zz = (3.0 * d.z * d.z) - 1.0;
    float xz = d.x * d.z;
    float xxmyy = (d.x * d.x) - (d.y * d.y);

    float r = probe.Lane0.x +
        (probe.Lane0.y * d.y + probe.Lane0.z * d.z + probe.Lane0.w * d.x) * l1Weight +
        (probe.Lane1.x * xy + probe.Lane1.y * yz + probe.Lane1.z * zz + probe.Lane1.w * xz + probe.Lane2.x * xxmyy) * l2Weight;

    float g = probe.Lane2.y +
        (probe.Lane2.z * d.y + probe.Lane2.w * d.z + probe.Lane3.x * d.x) * l1Weight +
        (probe.Lane3.y * xy + probe.Lane3.z * yz + probe.Lane3.w * zz + probe.Lane4.x * xz + probe.Lane4.y * xxmyy) * l2Weight;

    float b = probe.Lane4.z +
        (probe.Lane4.w * d.y + probe.Lane5.x * d.z + probe.Lane5.y * d.x) * l1Weight +
        (probe.Lane5.z * xy + probe.Lane5.w * yz + probe.Lane6.x * zz + probe.Lane6.y * xz + probe.Lane6.z * xxmyy) * l2Weight;

    return max(float3(0.0, 0.0, 0.0), clamp(float3(r, g, b), -H8_CUSTOM_LIGHT_PROBE_MAX_LUMA, H8_CUSTOM_LIGHT_PROBE_MAX_LUMA));
}

float3 H8CustomLightProbeSampleNearest(float3 gridCoord, float3 normalWS, uint resolution, float l1Weight, float l2Weight)
{
    uint3 coord = H8CustomLightProbeClampCoord((int3)floor(gridCoord + 0.5), resolution);
    return H8CustomLightProbeEvaluate(_H8CustomLightProbeGrid[H8CustomLightProbeIndex(coord, resolution)], normalWS, l1Weight, l2Weight);
}

float3 H8CustomLightProbeSampleTrilinear(float3 gridCoord, float3 normalWS, uint resolution, float l1Weight, float l2Weight)
{
    float3 baseFloat = floor(gridCoord);
    float3 t = saturate(gridCoord - baseFloat);
    int3 baseCoord = (int3)baseFloat;
    float3 result = float3(0.0, 0.0, 0.0);

    [unroll]
    for (uint z = 0u; z <= 1u; z++)
    {
        [unroll]
        for (uint y = 0u; y <= 1u; y++)
        {
            [unroll]
            for (uint x = 0u; x <= 1u; x++)
            {
                float3 corner = float3((float)x, (float)y, (float)z);
                float3 weight3 = lerp(1.0 - t, t, corner);
                float weight = weight3.x * weight3.y * weight3.z;
                uint3 coord = H8CustomLightProbeClampCoord(baseCoord + int3((int)x, (int)y, (int)z), resolution);
                H8CustomLightProbeDTO probe = _H8CustomLightProbeGrid[H8CustomLightProbeIndex(coord, resolution)];
                result += H8CustomLightProbeEvaluate(probe, normalWS, l1Weight, l2Weight) * weight;
            }
        }
    }

    return max(float3(0.0, 0.0, 0.0), result);
}

half3 H8CustomLightProbeResolveAmbient(float3 positionWS, half3 normalWS, half3 fallbackAmbient)
{
    float active = step(0.5, _H8CustomLightProbeGridState.x) * step(0.5, _H8InteriorGIProbeOrigin.w);
    float quality = saturate(isfinite(_H8InteriorGIProbeParams.z) ? _H8InteriorGIProbeParams.z : 0.0);
    float useGrid = active * H8CustomLightProbeSmooth01((quality - 0.12) * 5.5555553);
    if (useGrid <= H8_CUSTOM_LIGHT_PROBE_EPS)
        return max(fallbackAmbient, half3(0.0h, 0.0h, 0.0h));

    uint resolution = (uint)max((int)floor(_H8InteriorGIProbeParams.x + 0.5), 1);
    float cellSize = max(_H8InteriorGIProbeParams.y, H8_CUSTOM_LIGHT_PROBE_EPS);
    float3 local = positionWS - _H8InteriorGIProbeOrigin.xyz;
    float3 gridCoord = clamp(local * H8CustomLightProbeSafeRcp(cellSize), 0.0, max((float)resolution - 1.0, 0.0));
    float l1Weight = saturate(_H8InteriorGIProbeParams.w);
    float l2Weight = H8CustomLightProbeSmooth01((quality - 0.54) * 2.173913);
    float3 normal = H8CustomLightProbeSafeNormal((float3)normalWS, float3(0.0, 1.0, 0.0));
    float3 nearest = H8CustomLightProbeSampleNearest(gridCoord, normal, resolution, l1Weight, l2Weight);

#if defined(_MATH_LOD_LOW)
    float3 customAmbient = nearest;
#else
    float richWeight = H8CustomLightProbeSmooth01((quality - 0.46) * 1.8518518);
    float3 customAmbient = nearest;
    [branch]
    if (richWeight > H8_CUSTOM_LIGHT_PROBE_EPS)
    {
        float3 trilinear = H8CustomLightProbeSampleTrilinear(gridCoord, normal, resolution, l1Weight, l2Weight);
        customAmbient = lerp(nearest, trilinear, richWeight);
    }
#endif

    return (half3)lerp((float3)max(fallbackAmbient, half3(0.0h, 0.0h, 0.0h)), customAmbient, useGrid);
}

#endif
