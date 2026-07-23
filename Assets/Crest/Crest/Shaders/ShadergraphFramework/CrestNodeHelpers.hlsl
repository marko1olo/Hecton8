// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

#if UNITY_VERSION < 202320
float4 _CameraDepthTexture_TexelSize;
#endif

#include "OceanGraphConstants.hlsl"
#include "../OceanGlobals.hlsl"
#include "../OceanShaderHelpers.hlsl"
#include "../OceanVertHelpers.hlsl"

void CrestNodeLinearEyeDepth_float
(
	in const float i_rawDepth,
	out float o_linearDepth
)
{
	o_linearDepth = CrestLinearEyeDepth(i_rawDepth);
}

void CrestNodeMultiSampleDepth_float
(
	in const float i_rawDepth,
	in const float2 i_positionNDC,
	out float o_rawDepth
)
{
	o_rawDepth = CREST_MULTISAMPLE_SCENE_DEPTH(i_positionNDC, i_rawDepth);
}


void CrestNodeLodAlphaInterpolationFactor_float(
	in const float3 i_worldPos,
	in const float i_cascadeScale,
	out float o_lodAlphaInterpolationFactor
)
{
	float2 offsetFromCenter = abs(float2(i_worldPos.x - _OceanCenterPosWorld.x, i_worldPos.z - _OceanCenterPosWorld.z));
	float taxicab_norm = max(offsetFromCenter.x, offsetFromCenter.y);
	o_lodAlphaInterpolationFactor = taxicab_norm / i_cascadeScale - 1.0;
}

void CrestNodeComputeLodAlpha_float(
	in const float i_lodAlpha,
	in const float i_meshScaleAlpha,
	out float o_lodAlpha
)
{
	o_lodAlpha = ComputeLodAlpha(i_lodAlpha, i_meshScaleAlpha);
}
