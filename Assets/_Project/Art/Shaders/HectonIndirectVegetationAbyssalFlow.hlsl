#ifndef HECTON_INDIRECT_VEGETATION_ABYSSAL_FLOW_INCLUDED
#define HECTON_INDIRECT_VEGETATION_ABYSSAL_FLOW_INCLUDED

// Abyssal flow field, shared by Hecton_IndirectVegetation (ForwardLit) and its DepthOnly /
// MotionVectors / Shadow passes.
//
// The three non-lit passes did not omit this because someone judged it too expensive - they omitted
// it because none of its seven uniforms was ever declared in those files, so the function could not
// have compiled there. ResolveFlowSynchronyOffset consequently summed ONE flow field in the shadow,
// depth and motion passes and TWO in the lit pass, which is a different vertex displacement wherever
// the abyssal field is non-zero.
//
// Declaring them costs no authoring: every one is a plain global set from C#
// (HectonFluidEngine, HectonBoidController, GpuScatterLodManager), not a material property, so the
// three extra passes receive the same values the lit pass already receives, with no material to
// keep in sync.
//
// Requires URP Core.hlsl (TEXTURE3D / SAMPLER) to be included first.
//
// Body copied verbatim from the lit pass, which is the reference. Keep it that way.

StructuredBuffer<float4> _AbyssalFlowFieldResult;

float4 _AbyssalGridResolution;
float4 _AbyssalFlowCenter;
float4 _AbyssalFlowSpacing;
float4 _AbyssalFlowTextureParams;
float _AbyssalFlowTextureActive;

TEXTURE3D(_AbyssalFlowFieldTexture);
SAMPLER(sampler_AbyssalFlowFieldTexture);

float3 ResolveAbyssalFlowField(float3 positionWS)
{
    if (_AbyssalFlowTextureActive > 0.5 && _AbyssalFlowTextureParams.y > 0.001)
    {
        float3 uvw = (positionWS - _AbyssalFlowCenter.xyz) * rcp(_AbyssalFlowTextureParams.y) + 0.5;
        if (all(uvw >= float3(0.0, 0.0, 0.0)) && all(uvw <= float3(1.0, 1.0, 1.0)))
        {
            float3 textureFlow = _AbyssalFlowFieldTexture.SampleLevel(sampler_AbyssalFlowFieldTexture, uvw, 0).xyz;
            if (dot(textureFlow, textureFlow) < 1.0e+32)
                return textureFlow;
        }
    }

    int resolutionX = (int)max(_AbyssalGridResolution.x, 0.0);
    int resolutionY = (int)max(_AbyssalGridResolution.y, 0.0);
    int resolutionZ = (int)max(_AbyssalGridResolution.z, 0.0);
    int nodeCount = (int)max(_AbyssalGridResolution.w, 0.0);
    if (resolutionX <= 1 || resolutionY <= 1 || resolutionZ <= 1 || nodeCount <= 0)
        return float3(0.0, 0.0, 0.0);

    float horizontalCellSize = max(_AbyssalFlowSpacing.x, 0.001);
    float verticalCellSize = max(_AbyssalFlowSpacing.y, 0.001);
    int3 halfExtent = int3(resolutionX >> 1, resolutionY >> 1, resolutionZ >> 1);
    float3 localPosition = positionWS - _AbyssalFlowCenter.xyz;
    int3 coord = int3(round(float3(
        localPosition.x / horizontalCellSize,
        localPosition.y / verticalCellSize,
        localPosition.z / horizontalCellSize))) + halfExtent;
    if (coord.x < 0 || coord.y < 0 || coord.z < 0 || coord.x >= resolutionX || coord.y >= resolutionY || coord.z >= resolutionZ)
        return float3(0.0, 0.0, 0.0);

    int index = coord.y * resolutionX * resolutionZ + coord.z * resolutionX + coord.x;
    if (index < 0 || index >= nodeCount)
        return float3(0.0, 0.0, 0.0);

    float3 flow = _AbyssalFlowFieldResult[index].xyz;
    return dot(flow, flow) < 1.0e+32 ? flow : float3(0.0, 0.0, 0.0);
}

#endif // HECTON_INDIRECT_VEGETATION_ABYSSAL_FLOW_INCLUDED
