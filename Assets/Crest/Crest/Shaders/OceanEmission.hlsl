// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

#ifndef CREST_OCEAN_EMISSION_INCLUDED
#define CREST_OCEAN_EMISSION_INCLUDED

// ═══════════════════════════════════════════════════════════════════
// FOVEATED RENDERING FALLBACK — Unity 6 Compatibility Fix
//
// Unity 6 (URP 17+) removed or relocated foveated rendering macros
// that were previously available in earlier URP versions.
//
// FoveatedRemapLinearToNonUniform(uv):
//   Remaps linear UV coordinates to non-uniform space for foveated
//   rendering on VR devices. When foveated rendering is not active
//   or the macro is not defined, it should be a no-op (identity).
//
// This guard ensures Crest compiles on:
//   - Unity 6 / URP 17+ (macro removed)
//   - Unity 2022 / URP 14-16 (macro may or may not exist)
//   - D3D11, Vulkan, Metal, OpenGL (all backends)
//   - Non-VR builds where _FOVEATED_RENDERING_NON_UNIFORM is never defined
//
// The #ifndef check is safe because:
//   - If the macro IS defined by a future URP version, we don't override it.
//   - If it's NOT defined, we provide a no-op fallback.
//   - CREST_MULTISAMPLE_SCENE_DEPTH and other Crest macros that call this
//     will compile without modification.
// ═══════════════════════════════════════════════════════════════════

#ifndef FoveatedRemapLinearToNonUniform
    #define FoveatedRemapLinearToNonUniform(uv) (uv)
#endif

// ═══════════════════════════════════════════════════════════════════
// Additional foveated rendering macros that may be referenced by
// Crest shaders in other .hlsl files. Define fallbacks for all
// known variants to prevent compile errors across the Crest package.
// ═══════════════════════════════════════════════════════════════════

#ifndef FoveatedRemapNonUniformToLinear
    #define FoveatedRemapNonUniformToLinear(uv) (uv)
#endif

#ifndef FoveatedRemapDensity
    #define FoveatedRemapDensity(uv) (1.0)
#endif

// ═══════════════════════════════════════════════════════════════════
// _FOVEATED_RENDERING_NON_UNIFORM keyword safety:
//
// Some Crest code paths check #if defined(_FOVEATED_RENDERING_NON_UNIFORM)
// before calling FoveatedRemapLinearToNonUniform. This keyword is set
// by URP when foveated rendering is active on the platform.
//
// On D3D11/MX350 and non-VR platforms, this keyword is NEVER defined,
// so foveated code paths are compiled out at the preprocessor level.
// The fallback macros above are a safety net for code paths that
// call the function WITHOUT checking the keyword first (like Crest does
// in the transparency/refraction section below).
//
// No action needed for _FOVEATED_RENDERING_NON_UNIFORM itself —
// it's a shader_feature/multi_compile keyword managed by URP, not a macro.
// ═══════════════════════════════════════════════════════════════════


half3 ScatterColour
(
	in const half i_surfaceOceanDepth,
	in const float i_shadow,
	in const half sss,
	in const half3 i_view,
	in const half3 i_ambientLighting,
	in const half3 i_lightDir,
	in const half3 i_lightCol,
	in const half3 i_additionalLightCol,
	in const bool i_underwater
)
{
	// base colour
	float v = abs(i_view.y);
	// Previously caused rendering artifacts. See issue #1040.
	half3 col = lerp(_DiffuseGrazing, _Diffuse, v);

#if _SHADOWS_ON
	col = lerp(_DiffuseShadow, col, i_shadow);
#endif

#if _SUBSURFACESCATTERING_ON
	{
#if _SUBSURFACESHALLOWCOLOUR_ON
		float shallowness = pow(1. - saturate(i_surfaceOceanDepth / _SubSurfaceDepthMax), _SubSurfaceDepthPower);
		half3 shallowCol = _SubSurfaceShallowCol;
#if _SHADOWS_ON
		shallowCol = lerp(_SubSurfaceShallowColShadow, shallowCol, i_shadow);
#endif
		col = lerp(col, shallowCol, shallowness);
#endif

		col *= i_ambientLighting + i_lightCol;

		// Approximate subsurface scattering - add light when surface faces viewer. Use geometry normal - don't need high freqs.
		half towardsSun = pow(max(0., dot(i_lightDir, -i_view)), _SubSurfaceSunFallOff);
		half3 subsurface = (_SubSurfaceBase + _SubSurfaceSun * towardsSun) * _SubSurfaceColour.rgb * i_lightCol * i_shadow;
		if (!i_underwater)
		{
			subsurface *= (1.0 - v * v) * sss;
#if _ADDITIONAL_LIGHTS
			subsurface += _SubSurfaceColour.rgb * i_additionalLightCol;
#endif
		}
		col += subsurface;
	}
#endif // _SUBSURFACESCATTERING_ON

	return col;
}


#if _CAUSTICS_ON
void ApplyCaustics
(
	in const WaveHarmonic::Crest::TiledTexture i_causticsTexture,
	in const WaveHarmonic::Crest::TiledTexture i_distortionTexture,
	in const int2 i_positionSS,
	in const float3 i_scenePos,
	in const half3 i_lightDir,
	in const float i_sceneZ,
	in const bool i_underwater,
	inout half3 io_sceneColour,
	in const int i_sliceIndex,
	in const CascadeParams cascadeData
)
{
	const float3 scenePosUV = WorldToUV(i_scenePos.xz, cascadeData, i_sliceIndex);

	float3 disp = 0.0;
	SampleDisplacements(_LD_TexArray_AnimatedWaves, scenePosUV, 1.0, disp);
	half seaLevelOffset = _LD_TexArray_SeaFloorDepth.SampleLevel(LODData_linear_clamp_sampler, scenePosUV, 0.0).y;
	half waterHeight = _OceanCenterPosWorld.y + disp.y + seaLevelOffset;
	half sceneDepth = waterHeight - i_scenePos.y;
	float mipLod = log2(max(i_sceneZ, 1.0)) + abs(sceneDepth - _CausticsFocalDepth) / _CausticsDepthOfField;
	float2 lightProjection = i_lightDir.xz * sceneDepth / (4.0 * i_lightDir.y);

	float3 cuv1 = 0.0; float3 cuv2 = 0.0;
	{
		float2 surfacePosXZ = i_scenePos.xz;
		float surfacePosScale = 1.37;

#if CREST_FLOATING_ORIGIN
		surfacePosXZ -= i_causticsTexture.FloatingOriginOffset();
		surfacePosScale = 1.0;
#endif

		surfacePosXZ += lightProjection;

		cuv1 = float3
		(
			surfacePosXZ / i_causticsTexture._scale + float2(0.044 * _CrestTime + 17.16, -0.169 * _CrestTime),
			mipLod
		);
		cuv2 = float3
		(
			surfacePosScale * surfacePosXZ / i_causticsTexture._scale + float2(0.248 * _CrestTime, 0.117 * _CrestTime),
			mipLod
		);
	}

#if !defined(UNITY_SINGLE_PASS_STEREO) && !defined(UNITY_STEREO_INSTANCING_ENABLED)
	if (i_underwater)
#endif
	{
		float2 surfacePosXZ = i_scenePos.xz;

#if CREST_FLOATING_ORIGIN
		surfacePosXZ -= i_distortionTexture.FloatingOriginOffset();
#endif

		surfacePosXZ += lightProjection;

		half2 causticN = _CausticsDistortionStrength * UnpackNormal(i_distortionTexture.Sample(surfacePosXZ / i_distortionTexture._scale)).xy;
		cuv1.xy += 1.30 * causticN;
		cuv2.xy += 1.77 * causticN;
	}

	half causticsStrength = _CausticsStrength;

#if _SHADOWS_ON
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
	{
		{
			float4 shadowCoord = TransformWorldToShadowCoord(i_scenePos);
			Light mainLight = GetMainLight(TransformWorldToShadowCoord(i_scenePos));
			causticsStrength *= mainLight.shadowAttenuation;
		}
	}
#endif // UNIVERSAL_PIPELINE_CORE_INCLUDED
#endif // _SHADOWS_ON

	io_sceneColour.xyz *= 1.0 + causticsStrength *
	(
		0.5 * i_causticsTexture.SampleLevel(cuv1.xy, cuv1.z).xyz +
		0.5 * i_causticsTexture.SampleLevel(cuv2.xy, cuv2.z).xyz -
		_CausticsTextureAverage
	);
}
#endif // _CAUSTICS_ON

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
half3 OceanEmission
(
	in const half3 i_view,
	in const half3 i_n_pixel,
	in const float3 i_lightDir,
	in const real3 i_grabPosXYW,
	in const float i_pixelZ,
	const float i_rawPixelZ,
	in const half2 i_uvDepth,
	in const int2 i_positionSS,
	in const float i_sceneZ,
	const float i_rawDepth,
	in const half3 i_bubbleCol,
	in const bool i_underwater,
	in const half3 i_scatterCol,
#if CREST_WATER_VOLUME
	in const bool i_backface,
#endif
	in const CascadeParams cascadeData0,
	in const CascadeParams cascadeData1
)
{
	half3 col = i_scatterCol;

	// underwater bubbles reflect in light
	col += i_bubbleCol;

#if _TRANSPARENCY_ON

	const half2 uvBackground = i_grabPosXYW.xy / i_grabPosXYW.z;
	half3 sceneColour;
	half3 alpha = 0.;
	float depthFogDistance;

	half2 refractOffset = _RefractionStrength * i_n_pixel.xz;
	if (!i_underwater)
	{
		refractOffset *= min(1.0, 0.5 * (i_sceneZ - i_pixelZ)) / i_sceneZ;
	}

	float2 uvDepthRefract = i_uvDepth + refractOffset;
	uvDepthRefract = FoveatedRemapLinearToNonUniform(uvDepthRefract);

	float rawDepth = CREST_SAMPLE_SCENE_DEPTH_X(uvDepthRefract);

	bool caustics = true;
#if CREST_WATER_VOLUME_HAS_BACKFACE
	bool backface = ApplyVolumeToOceanSurfaceRefractions(i_positionSS + (refractOffset * _ScreenSize.xy), i_rawDepth, i_underwater, rawDepth, caustics);
#endif

	if (!i_underwater)
	{
		half2 uvBackgroundRefract;
		float sceneZ = i_sceneZ;

		if (rawDepth < i_rawPixelZ)
		{
			uvBackgroundRefract = uvBackground + refractOffset;
			uvBackgroundRefract = FoveatedRemapLinearToNonUniform(uvBackgroundRefract);

#if CREST_WATER_VOLUME_HAS_BACKFACE
			if (!backface)
#endif
			{
				rawDepth = CREST_MULTISAMPLE_SCENE_DEPTH(uvBackgroundRefract, rawDepth);
			}
			sceneZ = CrestLinearEyeDepth(rawDepth);
			depthFogDistance = sceneZ - i_pixelZ;
		}
		else
		{
			uvBackgroundRefract = uvBackground;
			uvBackgroundRefract = FoveatedRemapLinearToNonUniform(uvBackgroundRefract);

			rawDepth = i_rawDepth;
#if CREST_WATER_VOLUME_HAS_BACKFACE
			if (!i_backface)
#endif
			{
				rawDepth = CREST_MULTISAMPLE_SCENE_DEPTH(uvBackground, rawDepth);
			}
			sceneZ = CrestLinearEyeDepth(rawDepth);
			depthFogDistance = max(sceneZ - i_pixelZ, 0.0);
		}

		sceneColour = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvBackgroundRefract).rgb;
#if _CAUSTICS_ON
#if CREST_WATER_VOLUME_HAS_BACKFACE
		if (caustics)
#endif
		{
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED)
			float3 scenePos = _WorldSpaceCameraPos - i_view * sceneZ / dot(UNITY_MATRIX_I_V._13_23_33, i_view);
#else
			float3 scenePos = ComputeWorldSpacePosition(uvBackgroundRefract, rawDepth, UNITY_MATRIX_I_VP);
#endif
			ApplyCaustics(_CausticsTiledTexture, _CausticsDistortionTiledTexture, uvBackgroundRefract * _ScreenSize.xy, scenePos, i_lightDir, sceneZ, i_underwater, sceneColour, _LD_SliceIndex + 1, cascadeData1);
		}
#endif
		alpha = 1.0 - exp(-_DepthFogDensity.xyz * depthFogDistance);
	}
	else
	{
		const half2 uvBackgroundRefract = rawDepth < i_rawPixelZ ? uvBackground + refractOffset : uvBackground;
		sceneColour = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvBackgroundRefract).rgb;
		depthFogDistance = i_pixelZ;
	}

	col = lerp(sceneColour, col, alpha);

#endif // _TRANSPARENCY_ON

	return col;
}
#endif // UNIVERSAL_PIPELINE_CORE_INCLUDED

#endif // CREST_OCEAN_EMISSION_INCLUDED