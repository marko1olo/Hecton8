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
int _HectonUseVisibleMatrixStream;
float _HectonImpostorTimeSeconds;
float _HectonImpostorFadeOutSeconds;

float3 HectonSafeNormalize(float3 value, float3 fallback)
{
    float lenSq = dot(value, value);
    return lenSq > 0.000001 ? value * rsqrt(lenSq) : fallback;
}

float3 HectonImpostorDirection(uint index)
{
    if (index == 0u) return float3(0.9238795, 0.3826834, 0.0);
    if (index == 1u) return float3(0.0, 0.3826834, 0.9238795);
    if (index == 2u) return float3(-0.9238795, 0.3826834, 0.0);
    if (index == 3u) return float3(0.0, 0.3826834, -0.9238795);
    if (index == 4u) return float3(0.9238795, -0.3826834, 0.0);
    if (index == 5u) return float3(0.0, -0.3826834, 0.9238795);
    if (index == 6u) return float3(-0.9238795, -0.3826834, 0.0);
    return float3(0.0, -0.3826834, -0.9238795);
}

void HectonImpostorSelectViews(float3 objectToCameraDir, out uint primary, out uint secondary, out float blend01)
{
    float best = -2.0;
    float second = -2.0;
    primary = 0u;
    secondary = 0u;

    [unroll]
    for (uint i = 0u; i < 8u; i++)
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

    blend01 = saturate(1.0 - (best - second) * 3.25);
}

float HectonInterleavedGradientNoise(float2 pixelPosition)
{
    float2 p = floor(pixelPosition);
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}

float2 HectonImpostorAtlasUv(float2 uv, uint viewIndex)
{
    uint x = viewIndex & 3u;
    uint y = viewIndex >> 2;
    return uv * float2(0.25, 0.25) + float2((float)x * 0.25, (float)y * 0.25);
}

float3 HectonDecodeImpostorNormal(float4 normalDepthSample, float normalStrength)
{
    float3 normalWS = normalDepthSample.rgb * 2.0 - 1.0;
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
