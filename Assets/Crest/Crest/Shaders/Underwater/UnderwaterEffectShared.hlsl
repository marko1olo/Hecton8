// Crest Ocean System

// Copyright 2021 Wave Harmonic Ltd

#ifndef CREST_UNDERWATER_EFFECT_SHARED_INCLUDED
#define CREST_UNDERWATER_EFFECT_SHARED_INCLUDED

float2 _HorizonNormal;
float _HectonUnderwaterHasBlueNoiseTex;
float _HectonUnderwaterApplyAcesDither;
float _HectonUnderwaterDitherStrength;
float _HectonUnderwaterFrameIndex;
float _HectonUnderwaterNoirPower;
float4 _BlueNoiseTex_TexelSize;

TEXTURE2D(_BlueNoiseTex);
SAMPLER(sampler_BlueNoiseTex);

static const float k_HectonBayer4x4[16] =
{
	0.0, 8.0, 2.0, 10.0,
	12.0, 4.0, 14.0, 6.0,
	3.0, 11.0, 1.0, 9.0,
	15.0, 7.0, 13.0, 5.0
};
static const float2 k_HectonBlueNoiseR2Offset = float2(0.75487766, 0.56984029);

float ResolveHectonBayer4x4(int2 positionSS)
{
	int x = positionSS.x & 3;
	int y = positionSS.y & 3;
	return (k_HectonBayer4x4[y * 4 + x] + 0.5) * (1.0 / 16.0);
}

float ResolveHectonBlueNoise(int2 positionSS)
{
	float blueNoiseAvailable = step(0.5, _HectonUnderwaterHasBlueNoiseTex) * step(0.0001, _BlueNoiseTex_TexelSize.z);
	float fallback = ResolveHectonBayer4x4(positionSS);
	if (blueNoiseAvailable <= 0.0)
	{
		return fallback;
	}

	float2 screenUV = (positionSS + 0.5) * _ScaledScreenParams.zw;
	float2 tileScale = max(_ScaledScreenParams.xy * _BlueNoiseTex_TexelSize.xy, float2(1.0, 1.0));
	float2 temporalOffset = frac(_HectonUnderwaterFrameIndex * k_HectonBlueNoiseR2Offset);
	float2 blueNoiseUV = screenUV * tileScale + temporalOffset;
	return SAMPLE_TEXTURE2D_LOD(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV, 0).r;
}

half ResolveHectonNoirFogFactor(float fogDistance)
{
	float densityScalar = max(dot(_DepthFogDensity.xyz, float3(0.33333334, 0.33333334, 0.33333334)), 0.0001);
	float fogRaw = saturate(1.0 - exp(-max(fogDistance, 0.0) * densityScalar));
	return pow((half)fogRaw, max((half)_HectonUnderwaterNoirPower, 1.0h));
}

half3 ApplyHectonUnderwaterDither(half3 color, int2 positionSS)
{
	float bn = ResolveHectonBlueNoise(positionSS);
	float ditherOffset = (bn - 0.5) * (1.0 / 255.0) * _HectonUnderwaterDitherStrength;
	return color + (half3)ditherOffset;
}

float4 DebugRenderOceanMask(const bool isOceanSurface, const bool isUnderwater, const float mask, const float3 sceneColour)
{
	if (isOceanSurface)
	{
		return float4(sceneColour * float3(mask >= CREST_MASK_ABOVE_SURFACE, mask <= CREST_MASK_BELOW_SURFACE, 0.0), 1.0);
	}
	else
	{
		return float4(sceneColour * float3(isUnderwater * 0.5, (1.0 - isUnderwater) * 0.5, 1.0), 1.0);
	}
}

float4 DebugRenderStencil(float3 sceneColour)
{
	float3 stencil = 1.0;
#if CREST_WATER_VOLUME_FRONT_FACE
	stencil = float3(1.0, 0.0, 0.0);
#elif CREST_WATER_VOLUME_BACK_FACE
	stencil = float3(0.0, 1.0, 0.0);
#elif CREST_WATER_VOLUME_FULL_SCREEN
	stencil = float3(0.0, 0.0, 1.0);
#endif
	return float4(sceneColour * stencil, 1.0);
}

float MeniscusSampleOceanMask(const float mask, const int2 positionSS, const float2 offset, const float magnitude, const float scale)
{
	float2 uv = positionSS + offset * magnitude
#if CREST_WATER_VOLUME
	* scale
#endif
	;

	float newMask = LOAD_TEXTURE2D_X(_CrestOceanMaskTexture, uv).r;

#if CREST_UNDERWATER_BEFORE_TRANSPARENT
	// Normalize mask.
	newMask = clamp(newMask, -1.0, 1.0);
#endif

#if CREST_WATER_VOLUME
	// No mask means no underwater effect so ignore the value.
	return (newMask == CREST_MASK_NONE ? mask : newMask);
#endif
	return newMask;
}

half ComputeMeniscusWeight(const int2 positionSS, float mask, const float2 horizonNormal, const float meniscusDepth)
{
	float weight = 1.0;
#if CREST_MENISCUS
#if !_FULL_SCREEN_EFFECT

#if CREST_UNDERWATER_BEFORE_TRANSPARENT
	// Normalize mask.
	mask = clamp(mask, -1.0, 1.0);
#endif

	// Render meniscus by checking the mask along the horizon normal which is flipped using the surface normal from
	// mask. Adding the mask value will flip the UV when mask is below surface.
	float2 offset = (float2)-mask * horizonNormal;
	float multiplier = 0.9;

#if CREST_WATER_VOLUME
	// The meniscus at the boundary can be at a distance. We need to scale the offset as 1 pixel at a distance is much
	// larger than 1 pixel up close.
	const float scale = 1.0 - saturate(meniscusDepth / MENISCUS_MAXIMUM_DISTANCE);

	// Exit early.
	if (scale == 0.0)
	{
		return 1.0;
	}
#else
	// Dummy value.
	const float scale = 0.0;
#endif

	// Sample three pixels along the normal. If the sample is different than the current mask, apply meniscus.
	// Offset must be added to positionSS as floats.
	weight *= (MeniscusSampleOceanMask(mask, positionSS, offset, 1.0, scale) != mask) ? multiplier : 1.0;
	weight *= (MeniscusSampleOceanMask(mask, positionSS, offset, 2.0, scale) != mask) ? multiplier : 1.0;
	weight *= (MeniscusSampleOceanMask(mask, positionSS, offset, 3.0, scale) != mask) ? multiplier : 1.0;
#endif // _FULL_SCREEN_EFFECT
#endif // CREST_MENISCUS
	return weight;
}

void GetOceanSurfaceAndUnderwaterData
(
	const float4 positionCS,
	const int2 positionSS,
	const float rawOceanDepth,
	const float mask,
	inout float rawDepth,
	inout bool isOceanSurface,
	inout bool isUnderwater,
	inout bool hasCaustics,
	inout float sceneZ,
	const float oceanDepthTolerance
)
{
	hasCaustics = rawDepth != 0.0;
	isOceanSurface = false;
	isUnderwater = mask <= CREST_MASK_BELOW_SURFACE;

#if defined(CREST_WATER_VOLUME_HAS_BACKFACE) || defined(CREST_WATER_VOLUME_BACK_FACE)
	const float rawGeometryDepth =
#if CREST_WATER_VOLUME_HAS_BACKFACE
	// 3D has a back face texture for the depth.
	LOAD_DEPTH_TEXTURE_X(_CrestWaterVolumeBackFaceTexture, positionSS);
#else
	// Volume is rendered using the back face so that is the depth.
	positionCS.z;
#endif // CREST_WATER_VOLUME_HAS_BACKFACE
	;

	// Use backface depth if closest.
	if (rawDepth < rawGeometryDepth && rawOceanDepth < rawGeometryDepth && isUnderwater)
	{
		// Cancels out caustics.
		hasCaustics = false;
		rawDepth = rawGeometryDepth;
		// No need to multi-sample.
		sceneZ = CrestLinearEyeDepth(rawDepth);
		return;
	}
#endif // CREST_WATER_VOLUME

	// Merge ocean depth with scene depth.
	if (rawDepth < rawOceanDepth + oceanDepthTolerance)
	{
#if CREST_UNDERWATER_BEFORE_TRANSPARENT
		// Apply fog to culled tiles otherwise there will be no fog as ocean shader can only fog enabled tiles. And
		// only apply fog to culled tiles otherwise it will be fogged twice (second by ocean shader).
		isUnderwater = mask <= CREST_MASK_BELOW_SURFACE_CULLED;
#endif
		isOceanSurface = true;
		hasCaustics = false;
#if CREST_WATER_VOLUME_HAS_BACKFACE
		rawDepth = rawOceanDepth < rawGeometryDepth ? rawDepth : rawOceanDepth;
#else
		rawDepth = rawOceanDepth;
#endif
		sceneZ = CrestLinearEyeDepth(CREST_MULTILOAD_DEPTH(_CrestOceanMaskDepthTexture, positionSS, rawDepth));
	}
	else
	{
#if CREST_WATER_VOLUME_HAS_BACKFACE
		// If multi-loaded, the deepest depth could be behind the back face. Need a combined depth. But would also need
		// to redesign the passes to not render caustics on the back face.
		sceneZ = CrestLinearEyeDepth(rawDepth);
#else
		// For small water volumes this will have the opposite effect.
		sceneZ = CrestLinearEyeDepth(CREST_MULTILOAD_SCENE_DEPTH(positionSS, rawDepth));
#endif
	}
}

#ifdef CREST_OCEAN_EMISSION_INCLUDED
half3 ApplyUnderwaterEffect
(
	const int2 i_positionSS,
	const float3 scenePos,
	half3 sceneColour,
	const half3 lightCol,
	const float3 lightDir,
	const float sceneZ,
	const float fogDistance,
	const half3 view,
	const bool hasCaustics
)
{
	half3 scatterCol = 0.0;
	int sliceIndex = clamp(_CrestDataSliceOffset, 0, _SliceCount - 2);
	{
		// Offset slice so that we dont get high freq detail. But never use last lod as this has crossfading.
		const float3 uv_slice = WorldToUV(_WorldSpaceCameraPos.xz, _CrestCascadeData[sliceIndex], sliceIndex);

		half shadow = 1.0;
#if _SHADOWS_ON
		{
			// Camera should be at center of LOD system so no need for blending (alpha, weights, etc). This might not be
			// the case if there is large horizontal displacement, but the _CrestDataSliceOffset should help by setting a
			// large enough slice as minimum.
			shadow = _LD_TexArray_Shadow.SampleLevel(LODData_linear_clamp_sampler, uv_slice, 0.0).x;
			shadow = saturate(1.0 - shadow);
		}
#endif // _SHADOWS_ON

		half seaFloorDepth = CREST_OCEAN_DEPTH_BASELINE;
#if _SUBSURFACESHALLOWCOLOUR_ON
		{
			// compute scatter colour from cam pos. two scenarios this can be called:
			// 1. rendering ocean surface from bottom, in which case the surface may be some distance away. use the scatter
			//    colour at the camera, not at the surface, to make sure its consistent.
			// 2. for the underwater skirt geometry, we don't have the lod data sampled from the verts with lod transitions etc,
			//    so just approximate by sampling at the camera position.
			// this used to sample LOD1 but that doesnt work in last LOD, the data will be missing.
			SampleSeaDepth(_LD_TexArray_SeaFloorDepth, uv_slice, 1.0, seaFloorDepth);
		}
#endif // _SUBSURFACESHALLOWCOLOUR_ON

		{
			scatterCol = ScatterColour
			(
				seaFloorDepth,
				shadow,
				1.0, // SSS is not used for underwater yet. Calculated in SampleDisplacementsNormals which is costly.
				view,
				_CrestAmbientLighting,
				lightDir,
				lightCol,
				0.0, // No additional light support.
				true
			);
		}
	}

#if _CAUSTICS_ON
	if (hasCaustics)
	{
		ApplyCaustics
		(
			_CausticsTiledTexture,
			_CausticsDistortionTiledTexture,
			i_positionSS,
			scenePos,
			lightDir,
			sceneZ,
			true,
			sceneColour,
			sliceIndex,
			_CrestCascadeData[sliceIndex]
		);
	}
#endif // _CAUSTICS_ON

	half3 foggedScene = sceneColour * (half3)exp(-_DepthFogDensity.xyz * max(fogDistance, 0.0));
	half fogNoir = ResolveHectonNoirFogFactor(fogDistance);
	half3 underwaterColor = lerp(foggedScene, scatterCol, fogNoir);

	if (_HectonUnderwaterApplyAcesDither > 0.5)
	{
		underwaterColor = ApplyHectonUnderwaterDither(underwaterColor, i_positionSS);
	}

	return underwaterColor;
}
#endif // CREST_OCEAN_EMISSION_INCLUDED

void ApplyWaterVolumeToUnderwaterFogAndMeniscus(float4 positionCS, inout float fogDistance, inout float meniscusDepth)
{
#if CREST_WATER_VOLUME_FRONT_FACE
	float depth = CrestLinearEyeDepth(positionCS.z);
	// Meniscus is rendered at the boundary so use the geometry z.
	meniscusDepth = depth;
	fogDistance -= depth;
#endif
}

#endif // CREST_UNDERWATER_EFFECT_SHARED_INCLUDED
