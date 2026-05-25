#ifndef HECTON_CUSTOM_LIGHT_PROBE_GRID_INCLUDED
#define HECTON_CUSTOM_LIGHT_PROBE_GRID_INCLUDED

#define H8_CUSTOM_LIGHT_PROBE_EPS 0.0001
#define H8_CUSTOM_LIGHT_PROBE_MAX_LUMA 32.0
#define H8_CUSTOM_LIGHT_PROBE_MAX_RESOLUTION 128.0
#define H8_CUSTOM_LIGHT_PROBE_MAX_COUNT 2097152.0

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
float4 _H8CustomLightProbeGridState;  // x=active count, y=grid version, z=published capacity, w=buffer index

CBUFFER_START(HectonEnvironmentLighting)
    float4 _H8EnvironmentAmbientColor;           // xyz=GPU-resolved ambient, w=GlobalQualityWeight
    float4 _H8EnvironmentFogColor;               // xyz=biome/depth fog, w=deep gloom
    float4 _H8EnvironmentDirectionalLightColor;  // xyz=directional tint, w=biome weight
    float4 _H8EnvironmentScalarParams;           // x=sun, y=moon, z=SH coefficient count, w=SH quality
CBUFFER_END
StructuredBuffer<float> _HectonGIRelaySHBuffer;
float _H8EnvironmentDebugBlocks;

float H8CustomLightProbeSafeRcp(float value)
{
    return rcp(max(abs(value), H8_CUSTOM_LIGHT_PROBE_EPS));
}

float H8CustomLightProbeSafeScalar(float value, float fallbackValue)
{
    return isfinite(value) ? value : fallbackValue;
}

float3 H8CustomLightProbeSafeFloat3(float3 value, float3 fallbackValue)
{
    return all(isfinite(value)) ? value : fallbackValue;
}

float3 H8EnvironmentLightingEvaluateGlobalSH(float3 normalWS, float3 fallbackAmbient, float quality)
{
    float coeffCount = floor(H8CustomLightProbeSafeScalar(_H8EnvironmentScalarParams.z, 0.0) + 0.5);
    [branch]
    if (coeffCount < 27.0)
        return fallbackAmbient;

    float3 d = H8CustomLightProbeSafeFloat3(normalWS, float3(0.0, 1.0, 0.0));
    float lenSq = max(dot(d, d), H8_CUSTOM_LIGHT_PROBE_EPS);
    d *= rsqrt(lenSq);

    float l1Source = saturate((quality - 0.16) * 3.125);
    float l2Source = saturate((quality - 0.44) * 2.2727273);
    float l1Weight = l1Source * l1Source * (3.0 - 2.0 * l1Source);
    float l2Weight = l2Source * l2Source * (3.0 - 2.0 * l2Source);
    float xy = d.x * d.y;
    float yz = d.y * d.z;
    float zz = (3.0 * d.z * d.z) - 1.0;
    float xz = d.x * d.z;
    float xxmyy = (d.x * d.x) - (d.y * d.y);

    float3 sh;
    sh.x = _HectonGIRelaySHBuffer[0] +
        (_HectonGIRelaySHBuffer[1] * d.y + _HectonGIRelaySHBuffer[2] * d.z + _HectonGIRelaySHBuffer[3] * d.x) * l1Weight +
        (_HectonGIRelaySHBuffer[4] * xy + _HectonGIRelaySHBuffer[5] * yz + _HectonGIRelaySHBuffer[6] * zz + _HectonGIRelaySHBuffer[7] * xz + _HectonGIRelaySHBuffer[8] * xxmyy) * l2Weight;
    sh.y = _HectonGIRelaySHBuffer[9] +
        (_HectonGIRelaySHBuffer[10] * d.y + _HectonGIRelaySHBuffer[11] * d.z + _HectonGIRelaySHBuffer[12] * d.x) * l1Weight +
        (_HectonGIRelaySHBuffer[13] * xy + _HectonGIRelaySHBuffer[14] * yz + _HectonGIRelaySHBuffer[15] * zz + _HectonGIRelaySHBuffer[16] * xz + _HectonGIRelaySHBuffer[17] * xxmyy) * l2Weight;
    sh.z = _HectonGIRelaySHBuffer[18] +
        (_HectonGIRelaySHBuffer[19] * d.y + _HectonGIRelaySHBuffer[20] * d.z + _HectonGIRelaySHBuffer[21] * d.x) * l1Weight +
        (_HectonGIRelaySHBuffer[22] * xy + _HectonGIRelaySHBuffer[23] * yz + _HectonGIRelaySHBuffer[24] * zz + _HectonGIRelaySHBuffer[25] * xz + _HectonGIRelaySHBuffer[26] * xxmyy) * l2Weight;

    float shSource = saturate((quality - 0.22) * 2.7777777);
    float shWeight = shSource * shSource * (3.0 - 2.0 * shSource);
    return lerp(fallbackAmbient, max(sh, float3(0.0, 0.0, 0.0)), shWeight);
}

float3 H8EnvironmentLightingResolveFallback(float3 fallbackAmbient, float3 normalWS)
{
    float3 safeFallback = max(H8CustomLightProbeSafeFloat3(fallbackAmbient, float3(0.0, 0.0, 0.0)), float3(0.0, 0.0, 0.0));
    float3 gpuAmbient = max(H8CustomLightProbeSafeFloat3(_H8EnvironmentAmbientColor.rgb, safeFallback), float3(0.0, 0.0, 0.0));
    float quality = saturate(max(
        H8CustomLightProbeSafeScalar(_H8EnvironmentAmbientColor.w, 0.0),
        H8CustomLightProbeSafeScalar(_H8EnvironmentScalarParams.w, 0.0)));
    float gloom = saturate(H8CustomLightProbeSafeScalar(_H8EnvironmentFogColor.w, 0.0));
    float valid = step(H8_CUSTOM_LIGHT_PROBE_EPS, dot(gpuAmbient, float3(0.3333333, 0.3333333, 0.3333333)) + quality + gloom);
    return H8EnvironmentLightingEvaluateGlobalSH(normalWS, lerp(safeFallback, gpuAmbient, valid), quality);
}

float3 H8EnvironmentLightingApplyDebugBlocks(float3 baseColor, float3 positionWS)
{
    if (_H8EnvironmentDebugBlocks < 0.5)
        return baseColor;

    float band = frac(positionWS.x * 0.05);
    float3 ambientBlock = max(_H8EnvironmentAmbientColor.rgb, float3(0.0, 0.0, 0.0));
    float3 fogBlock = max(_H8EnvironmentFogColor.rgb, float3(0.0, 0.0, 0.0));
    float3 directionalBlock = max(_H8EnvironmentDirectionalLightColor.rgb, float3(0.0, 0.0, 0.0));
    float3 blockColor = band < 0.3333333 ? ambientBlock : (band < 0.6666667 ? fogBlock : directionalBlock);
    return lerp(baseColor, blockColor, 0.72);
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

uint H8CustomLightProbeClampIndex(uint index, uint activeCount)
{
    return min(index, max(activeCount, 1u) - 1u);
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

float3 H8CustomLightProbeSampleNearest(float3 gridCoord, float3 normalWS, uint resolution, uint activeCount, float l1Weight, float l2Weight)
{
    uint3 coord = H8CustomLightProbeClampCoord((int3)floor(gridCoord + 0.5), resolution);
    uint index = H8CustomLightProbeClampIndex(H8CustomLightProbeIndex(coord, resolution), activeCount);
    return H8CustomLightProbeEvaluate(_H8CustomLightProbeGrid[index], normalWS, l1Weight, l2Weight);
}

float3 H8CustomLightProbeSampleTrilinear(float3 gridCoord, float3 normalWS, uint resolution, uint activeCount, float l1Weight, float l2Weight)
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
                uint index = H8CustomLightProbeClampIndex(H8CustomLightProbeIndex(coord, resolution), activeCount);
                H8CustomLightProbeDTO probe = _H8CustomLightProbeGrid[index];
                result += H8CustomLightProbeEvaluate(probe, normalWS, l1Weight, l2Weight) * weight;
            }
        }
    }

    return max(float3(0.0, 0.0, 0.0), result);
}

half3 H8CustomLightProbeResolveAmbient(float3 positionWS, half3 normalWS, half3 fallbackAmbient)
{
    float3 safeFallback = H8EnvironmentLightingResolveFallback((float3)fallbackAmbient, (float3)normalWS);
    float activeCountSource = max(0.0, floor(H8CustomLightProbeSafeScalar(_H8CustomLightProbeGridState.x, 0.0) + 0.5));
    float capacityFloat = min(max(0.0, floor(H8CustomLightProbeSafeScalar(_H8CustomLightProbeGridState.z, activeCountSource) + 0.5)), H8_CUSTOM_LIGHT_PROBE_MAX_COUNT);
    float activeCountFloat = min(activeCountSource, capacityFloat);
    float published = step(0.5, H8CustomLightProbeSafeScalar(_H8InteriorGIProbeOrigin.w, 0.0));
    float active = step(0.5, activeCountFloat) * published;
    float quality = saturate(H8CustomLightProbeSafeScalar(_H8InteriorGIProbeParams.z, 0.0));
    float useGrid = active * H8CustomLightProbeSmooth01((quality - 0.12) * 5.5555553);
    if (useGrid <= H8_CUSTOM_LIGHT_PROBE_EPS)
        return (half3)H8EnvironmentLightingApplyDebugBlocks(safeFallback, positionWS);

    float resolutionFloat = min(max(floor(H8CustomLightProbeSafeScalar(_H8InteriorGIProbeParams.x, 1.0) + 0.5), 1.0), H8_CUSTOM_LIGHT_PROBE_MAX_RESOLUTION);
    uint resolution = (uint)resolutionFloat;
    uint activeCount = (uint)min(activeCountFloat, H8_CUSTOM_LIGHT_PROBE_MAX_COUNT);
    uint requiredCount = resolution * resolution * resolution;
    if (activeCount < requiredCount || capacityFloat < (float)requiredCount)
        return (half3)H8EnvironmentLightingApplyDebugBlocks(safeFallback, positionWS);

    float cellSize = max(H8CustomLightProbeSafeScalar(_H8InteriorGIProbeParams.y, 1.0), H8_CUSTOM_LIGHT_PROBE_EPS);
    float3 origin = H8CustomLightProbeSafeFloat3(_H8InteriorGIProbeOrigin.xyz, positionWS);
    float3 safePosition = H8CustomLightProbeSafeFloat3(positionWS, origin);
    float3 local = safePosition - origin;
    float3 gridCoord = clamp(local * H8CustomLightProbeSafeRcp(cellSize), 0.0, max((float)resolution - 1.0, 0.0));
    float l1Weight = saturate(H8CustomLightProbeSafeScalar(_H8InteriorGIProbeParams.w, 0.0));
    float l2Weight = H8CustomLightProbeSmooth01((quality - 0.54) * 2.173913);
    float3 normal = H8CustomLightProbeSafeNormal((float3)normalWS, float3(0.0, 1.0, 0.0));
    float3 nearest = H8CustomLightProbeSampleNearest(gridCoord, normal, resolution, activeCount, l1Weight, l2Weight);

    float richWeight = H8CustomLightProbeSmooth01((quality - 0.46) * 1.8518518);
    float3 customAmbient = nearest;
    [branch]
    if (richWeight > H8_CUSTOM_LIGHT_PROBE_EPS)
    {
        float3 trilinear = H8CustomLightProbeSampleTrilinear(gridCoord, normal, resolution, activeCount, l1Weight, l2Weight);
        customAmbient = lerp(nearest, trilinear, richWeight);
    }

    return (half3)H8EnvironmentLightingApplyDebugBlocks(lerp(safeFallback, customAmbient, useGrid), positionWS);
}

#endif
