#ifndef HECTON_IMPOSTOR_INCLUDED
#define HECTON_IMPOSTOR_INCLUDED

struct HectonImpostorGpuInstance
{
    float4 CenterFade;
    float4 SizeFlags;
};

struct HectonImpostorVertexResult
{
    float3 positionWS;
    float4 positionCS;
    float2 uv;
    float3 viewDirectionWS;
    float fade01;
    float fogFactor;
};

StructuredBuffer<HectonImpostorGpuInstance> _HectonImpostorInstances;
StructuredBuffer<float4x4> _HectonVisibleInstances;

float3 HectonSafeNormalize(float3 value, float3 fallback)
{
    float lenSq = dot(value, value);
    if (lenSq > 0.000001)
        return value * rsqrt(max(lenSq, 0.000001));
    return fallback;
}

float3 HectonImpostorDirection(uint index)
{
    if (index == 0u) return float3(0.3479853, 0.9375000, 0.0000000);
    if (index == 1u) return float3(-0.4298574, 0.8125000, 0.3937846);
    if (index == 2u) return float3(0.0634872, 0.6875000, -0.7234038);
    if (index == 3u) return float3(0.5030556, 0.5625000, 0.6561469);
    if (index == 4u) return float3(-0.8854725, 0.4375000, -0.1566276);
    if (index == 5u) return float3(0.8014981, 0.3125000, -0.5098475);
    if (index == 6u) return float3(-0.2550001, 0.1875000, 0.9485877);
    if (index == 7u) return float3(-0.4600059, 0.0625000, -0.8857134);
    if (index == 8u) return float3(0.9374849, -0.0625000, 0.3423680);
    if (index == 9u) return float3(-0.9079519, -0.1875000, 0.3747894);
    if (index == 10u) return float3(0.4026188, -0.3125000, -0.8603731);
    if (index == 11u) return float3(0.2691216, -0.4375000, 0.8580019);
    if (index == 12u) return float3(-0.7153543, -0.5625000, -0.4145624);
    if (index == 13u) return float3(0.7092467, -0.6875000, -0.1559259);
    if (index == 14u) return float3(-0.3352781, -0.8125000, 0.4768986);
    return float3(-0.0447198, -0.9375000, -0.3450998);
}

void HectonImpostorSelectViews(float3 objectToCameraDir, out uint primary, out uint secondary, out float blend01)
{
    float best = -2.0;
    float second = -2.0;
    primary = 0u;
    secondary = 0u;

    [unroll]
    for (uint i = 0u; i < 16u; i++)
    {
        float score = dot(objectToCameraDir, HectonImpostorDirection(i));
        if (score > best)
        {
            second = best;
            secondary = primary;
            best = score;
            primary = i;
        }
        else if (score > second)
        {
            second = score;
            secondary = i;
        }
    }

    float denominator = max(abs(best) + abs(second), 0.0001);
    blend01 = saturate((second / denominator) * 0.5 + 0.5);
}

float HectonInterleavedGradientNoise(float2 pixelPosition)
{
    float2 p = floor(pixelPosition);
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}

float2 HectonImpostorAtlasUv(float2 uv, uint viewIndex, float4 atlasGrid)
{
    uint columns = (uint)max(1.0, atlasGrid.x);
    uint x = viewIndex % columns;
    uint y = viewIndex / columns;
    float2 invGrid = max(atlasGrid.zw, float2(0.0001, 0.0001));
    return uv * invGrid + float2((float)x, (float)y) * invGrid;
}

float3 HectonDecodeImpostorNormal(float4 normalDepthSample, float normalStrength)
{
    float2 normalXY = normalDepthSample.rg * 2.0 - 1.0;
    float normalZ = sqrt(saturate(1.0 - dot(normalXY, normalXY)));
    float3 normalWS = float3(normalXY.x, normalZ, normalXY.y);
    normalWS = HectonSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    return HectonSafeNormalize(lerp(float3(0.0, 1.0, 0.0), normalWS, saturate(normalStrength)), float3(0.0, 1.0, 0.0));
}

HectonImpostorVertexResult HectonBuildImpostorVertex(float2 quadPosition, float2 uv, uint instanceID, float4 globalFloatingOffset)
{
    HectonImpostorGpuInstance instance;
    if (_HectonUseVisibleMatrixStream != 0)
    {
        float4x4 matrixValue = _HectonVisibleInstances[instanceID];
        float3 size = max(abs(float3(matrixValue._m00, matrixValue._m11, matrixValue._m22)), float3(0.5, 0.5, 0.5));
        float age01 = saturate((_HectonImpostorTimeSeconds - matrixValue._m33) * rcp(max(_HectonImpostorFadeOutSeconds, 0.001)));
        float fade01 = matrixValue._m30 < 0.0 ? (1.0 - age01) : age01;
        instance.CenterFade = float4(matrixValue._m03, matrixValue._m13, matrixValue._m23, fade01);
        instance.SizeFlags = float4(size, matrixValue._m32);
    }
    else
    {
        instance = _HectonImpostorInstances[instanceID];
    }

    float3 centerWS = instance.CenterFade.xyz + globalFloatingOffset.xyz;
    float3 toCamera = HectonSafeNormalize(_WorldSpaceCameraPos.xyz - centerWS, float3(0.0, 0.0, 1.0));
    float3 rightWS = HectonSafeNormalize(cross(float3(0.0, 1.0, 0.0), toCamera), float3(1.0, 0.0, 0.0));
    float3 upWS = HectonSafeNormalize(cross(toCamera, rightWS), float3(0.0, 1.0, 0.0));

    float width = max(max(instance.SizeFlags.x, instance.SizeFlags.z), 0.5);
    float height = max(instance.SizeFlags.y, 0.5);
    float3 positionWS = centerWS + rightWS * (quadPosition.x * width) + upWS * (quadPosition.y * height);
    float4 positionCS = TransformWorldToHClip(positionWS);

    HectonImpostorVertexResult result;
    result.positionWS = positionWS;
    result.positionCS = positionCS;
    result.uv = uv;
    result.viewDirectionWS = toCamera;
    result.fade01 = saturate(instance.CenterFade.w);
    result.fogFactor = ComputeFogFactor(positionCS.z);
    return result;
}

#endif
