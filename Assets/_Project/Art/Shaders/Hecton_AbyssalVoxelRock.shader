Shader "Hecton8/Environment/Hecton_AbyssalVoxelRock"
{
    Properties
    {
        [MainTexture] _Base_Map ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _Normal_Map ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _Mask_Map ("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        [NoScaleOffset] _AlbedoArray ("Voxel Albedo Array", 2DArray) = "" {}
        [NoScaleOffset] _NormalArray ("Voxel Normal Array", 2DArray) = "" {}
        _VoxelSandArrayIndex ("Voxel Sand Array Index", Float) = 0
        _VoxelRockArrayIndex ("Voxel Rock Array Index", Float) = 3
        _VoxelTriplanarScale ("Voxel Triplanar Scale", Float) = 0.08
        _VoxelTriplanarSharpness ("Voxel Triplanar Sharpness", Range(1, 12)) = 5
        _VoxelArrayNormalStrength ("Voxel Array Normal Strength", Range(0, 1)) = 0.85
        _VoxelStochasticStrength ("Voxel Anti-Tiling Stochastic Strength", Range(0, 1)) = 0.55
        [NoScaleOffset] _HectonMicroNormalTex("Micro Normal 128", 2D) = "bump" {}
        [NoScaleOffset] _FreshRockAlbedoMap ("Fresh Rock Albedo Map", 2D) = "white" {}
        [NoScaleOffset] _FreshRockNormalMap ("Fresh Rock Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _SiltLayerMap ("Horizontal Silt Layer Map", 2D) = "white" {}
        [NoScaleOffset] _CavityNoiseRamp ("Cavity AO Depth Noise Ramp", 2D) = "gray" {}
        [NoScaleOffset] _GeologyStrataAlbedoMap ("Baked Geology Strata Albedo", 2D) = "white" {}
        [NoScaleOffset] _GeologyStrataMraoMap ("Baked Geology MRAO Sediment", 2D) = "white" {}
        [NoScaleOffset] _BiomeFamilyTintVolume ("Visual Family 3D Tint Volume", 3D) = "white" {}
        _Instance_Color ("Instance Color", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling", Range(0.01, 4)) = 0.2
        _Metallic ("Metallic Scale", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        _SkirtSandTint ("Skirt Sand Tint", Color) = (0.42, 0.38, 0.31, 1)
        _SkirtBlendContrast ("Skirt Blend Contrast", Range(0.1, 4)) = 1.4
        _TerrainSeamFadeDistance ("Terrain Seam Fade Distance", Range(0.25, 3.5)) = 2
        _TerrainSeamBandMeters ("Terrain Seam Band Meters", Float) = 3.5
        _CutScarColor ("Cut Scar Color", Color) = (1.0, 0.42, 0.12, 1)
        _CutScarWarmColor ("Cut Scar Warm Color", Color) = (0.42, 0.06, 0.03, 1)
        _CutScarCharColor ("Cut Scar Char Color", Color) = (0.06, 0.03, 0.02, 1)
        _CutScarEmission ("Cut Scar Emission", Range(0, 8)) = 1.8
        _CutScarSharpness ("Cut Scar Sharpness", Range(0.5, 8)) = 2.2
        _CutScarDarkening ("Cut Scar Darkening", Range(0, 1)) = 0.24
        _ShadowScarErosion ("Shadow Scar Erosion", Range(0, 0.35)) = 0.14
        _SkirtDepthBias ("Skirt Depth Bias", Range(0, 0.01)) = 0.00035
        _CurvatureWearTint ("Curvature Wear Tint", Color) = (0.84, 0.81, 0.77, 1)
        _CurvatureEdgeWearStrength ("Curvature Edge Wear Strength", Range(0, 1)) = 0.24
        _CurvatureCavityDarkenStrength ("Curvature Cavity Darken Strength", Range(0, 1)) = 0.28
        _CurvatureContrast ("Curvature Contrast", Range(0.5, 4)) = 1.35
        _ProceduralDirtAge ("Procedural Dirt Age", Range(0, 1)) = 0.65
        _SiltStrength ("Procedural Silt Strength", Range(0, 1)) = 0.42
        _RustStrength ("Procedural Rust Strength", Range(0, 1)) = 0.26
        _SiltTint ("Procedural Silt Tint", Color) = (0.31, 0.30, 0.25, 1)
        _RustTint ("Procedural Rust Tint", Color) = (0.47, 0.16, 0.06, 1)
        _EnvironmentalWear("Environmental Wear", Range(0, 1)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _MicroNormalStrength("Micro Normal Strength", Range(0, 1)) = 0.22
        _MicroNormalTiling("Micro Normal Tiling", Range(4, 128)) = 56
        _StormRainDripAmplitude("Storm Rain Drip Amplitude", Range(0, 0.025)) = 0.0025
        _StormRainDripTiling("Storm Rain Drip Tiling", Range(0.5, 16)) = 4
        _StormRainDripSpeed("Storm Rain Drip Speed", Range(0, 8)) = 1.6
        _ChunkDissolveFade ("Chunk Dissolve Fade", Range(0, 1)) = 1
        _ChunkDissolveGlitchStrength ("Chunk Dissolve Glitch Strength", Range(0, 1)) = 0.18
        _ChunkDissolvePhosphorTint ("Chunk Dissolve Phosphor Tint", Color) = (0.04, 0.82, 0.18, 1)
        _FreshCutColorBoost ("Fresh Cut Color Boost", Range(1, 2)) = 1.18
        _FreshCutNormalBoost ("Fresh Cut Normal Boost", Range(1, 3)) = 1.45
        _OrganicDisplacementStrength ("Organic World Noise Strength", Range(0, 0.45)) = 0.18
        _OrganicDisplacementScale ("Organic World Noise Scale", Range(0.02, 4)) = 0.55
        _OrganicDisplacementFineScale ("Organic Fine Noise Scale", Range(0.05, 12)) = 2.8
        _OrganicDisplacementSeamBoost ("Organic Seam Boost", Range(0, 2)) = 0.65
        _ScreenSpaceNormalBevelStrength ("Screen Normal Bevel Strength", Range(0, 4)) = 1.35
        _ScreenSpaceNormalNoiseStrength ("Screen Normal Noise Strength", Range(0, 0.35)) = 0.08
        _ScreenSpaceNormalNoiseScale ("Screen Normal Noise Scale", Range(0.05, 8)) = 1.25
        _CavityAoNoiseStrength ("Cavity AO Noise Strength", Range(0, 1)) = 0.32
        _CavityAoDepthScale ("Cavity AO Depth Scale", Range(0.001, 4)) = 0.19
        _CaveMouthDisplacementStrength ("Cave Mouth GPU Jag Strength", Range(0, 0.35)) = 0.08
        _CaveMouthDisplacementScale ("Cave Mouth GPU Jag Scale", Range(0.05, 4)) = 0.85
        _CaveMouthPhosphorPulseStrength ("Cave Mouth Phosphor Pulse Strength", Range(0, 1)) = 0.18
        _CaveMouthPhosphorPulseScale ("Cave Mouth Phosphor Pulse Scale", Range(0.05, 8)) = 1.6
        _HorizontalSiltDustStrength ("Horizontal Silt Dust Strength", Range(0, 1)) = 0.48
        _HorizontalSiltDustSharpness ("Horizontal Silt Dust Sharpness", Range(1, 12)) = 5
        _HorizontalSiltDustTiling ("Horizontal Silt Dust Tiling", Range(0.01, 4)) = 0.22
        _GeologyStrataBlend ("Baked Geology Blend", Range(0, 1)) = 0
        _GeologyWorldOriginAup ("Baked Geology World Origin AUP", Vector) = (0, -800, 0, 0)
        _GeologyTileMeters ("Baked Geology Tile Meters XY", Vector) = (64, 95, 0, 0)
        _GeologyOreGlintStrength ("Baked Ore Glint Strength", Range(0, 2)) = 0.65
        _GeologySedimentStrength ("Baked Sediment Strength", Range(0, 1)) = 0.72
        _BiomeFamilyTintStrength ("Visual Family Tint Strength", Range(0, 1)) = 0.32
        _BiomeFamilyTintVolumeStrength ("Visual Family 3D Tint Strength", Range(0, 1)) = 0.35
        _BiomeFamilyTintVolumeWorldOrigin ("Visual Family 3D Tint Origin AUP", Vector) = (0, 0, 0, 0)
        _BiomeFamilyTintVolumeWorldSize ("Visual Family 3D Tint Size AUP", Vector) = (512, 256, 512, 0)
        _BiomeFamilySandTint ("Visual Family Sand Tint", Color) = (0.50, 0.46, 0.36, 1)
        _BiomeFamilyBasaltTint ("Visual Family Basalt Tint", Color) = (0.17, 0.18, 0.19, 1)
        _BiomeFamilyKelpTint ("Visual Family Kelp Tint", Color) = (0.18, 0.29, 0.18, 1)
        _BiomeFamilyBrineTint ("Visual Family Brine Tint", Color) = (0.16, 0.31, 0.33, 1)
        _BiomeFamilyVolcanicTint ("Visual Family Volcanic Tint", Color) = (0.38, 0.13, 0.08, 1)
        _BiomeFamilyCoralTint ("Visual Family Coral Tint", Color) = (0.45, 0.25, 0.20, 1)
        _BiomeFamilyAbyssalTint ("Visual Family Abyssal Tint", Color) = (0.12, 0.15, 0.18, 1)
        _BiomeFamilyAlienTint ("Visual Family Alien Tint", Color) = (0.13, 0.26, 0.29, 1)
        _LocalCausticStrength ("Local Caustic Strength", Range(0, 1)) = 0.22
        _LocalCausticScale ("Local Caustic Scale", Range(0.1, 4)) = 0.7
        _LocalCausticSpeed ("Local Caustic Speed", Range(0, 4)) = 0.36
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Instance_Color;
            float4 _SkirtSandTint;
            float4 _CutScarColor;
            float4 _CutScarWarmColor;
            float4 _CutScarCharColor;
            float4 _CurvatureWearTint;
            float4 _SiltTint;
            float4 _RustTint;
            float4 _RustSaltColor;
            float4 _ChunkDissolvePhosphorTint;
            float4 _BiomeFamilySandTint;
            float4 _BiomeFamilyBasaltTint;
            float4 _BiomeFamilyKelpTint;
            float4 _BiomeFamilyBrineTint;
            float4 _BiomeFamilyVolcanicTint;
            float4 _BiomeFamilyCoralTint;
            float4 _BiomeFamilyAbyssalTint;
            float4 _BiomeFamilyAlienTint;
            float4 _BiomeFamilyTintVolumeWorldOrigin;
            float4 _BiomeFamilyTintVolumeWorldSize;
            float _Tiling;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _SkirtBlendContrast;
            float _TerrainSeamFadeDistance;
            float _TerrainSeamBandMeters;
            float _CutScarEmission;
            float _CutScarSharpness;
            float _CutScarDarkening;
            float _ShadowScarErosion;
            float _SkirtDepthBias;
            float _CurvatureEdgeWearStrength;
            float _CurvatureCavityDarkenStrength;
            float _CurvatureContrast;
            float _ProceduralDirtAge;
            float _SiltStrength;
            float _RustStrength;
            float _EnvironmentalWear;
            float _MicroNormalStrength;
            float _MicroNormalTiling;
            float _StormRainDripAmplitude;
            float _StormRainDripTiling;
            float _StormRainDripSpeed;
            float _ChunkDissolveFade;
            float _ChunkDissolveGlitchStrength;
            float _FreshCutColorBoost;
            float _FreshCutNormalBoost;
            float _OrganicDisplacementStrength;
            float _OrganicDisplacementScale;
            float _OrganicDisplacementFineScale;
            float _OrganicDisplacementSeamBoost;
            float _ScreenSpaceNormalBevelStrength;
            float _ScreenSpaceNormalNoiseStrength;
            float _ScreenSpaceNormalNoiseScale;
            float _CavityAoNoiseStrength;
            float _CavityAoDepthScale;
            float _CaveMouthDisplacementStrength;
            float _CaveMouthDisplacementScale;
            float _CaveMouthPhosphorPulseStrength;
            float _CaveMouthPhosphorPulseScale;
            float _HorizontalSiltDustStrength;
            float _HorizontalSiltDustSharpness;
            float _HorizontalSiltDustTiling;
            float _GeologyStrataBlend;
            float4 _GeologyWorldOriginAup;
            float4 _GeologyTileMeters;
            float _GeologyOreGlintStrength;
            float _GeologySedimentStrength;
            float _BiomeFamilyTintStrength;
            float _BiomeFamilyTintVolumeStrength;
            float _LocalCausticStrength;
            float _LocalCausticScale;
            float _LocalCausticSpeed;
            float _VoxelSandArrayIndex;
            float _VoxelRockArrayIndex;
            float _VoxelTriplanarScale;
            float _VoxelTriplanarSharpness;
            float _VoxelArrayNormalStrength;
            float _VoxelStochasticStrength;
        CBUFFER_END

        float4 _SargassumCutMaskWorldRect;
        float4 _HectonDamageVolumeWorldMin;
        float4 _HectonDamageVolumeInvSize;
        float4 _HectonFloatingOriginOffset;
        float4 _HectonNoirResolveSettings;
        float4 _HectonNoirCausticsLayerA;
        float4 _HectonNoirCausticsLayerB;
        float4 _HectonNoirCausticsShape;
        float4 _HectonNoirCaveAttenuation;
        float _SargassumCutMaskActive;
        float _HectonDamageVolumeActive;
        int _HectonScatterBiomeInfluenceGridCount;
        float4 _HectonScatterBiomeInfluenceGridOrigin;
        float4 _HectonScatterBiomeInfluenceGridParams;
        float3 _LightDirection;

        StructuredBuffer<uint> _HectonScatterBiomeInfluenceGrid;

        TEXTURE2D(_Base_Map);
        SAMPLER(sampler_Base_Map);
        TEXTURE2D(_Normal_Map);
        SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_Mask_Map);
        SAMPLER(sampler_Mask_Map);
        TEXTURE2D_ARRAY(_AlbedoArray);
        SAMPLER(sampler_AlbedoArray);
        TEXTURE2D_ARRAY(_NormalArray);
        SAMPLER(sampler_NormalArray);
        TEXTURE2D(_FreshRockAlbedoMap);
        SAMPLER(sampler_FreshRockAlbedoMap);
        TEXTURE2D(_FreshRockNormalMap);
        SAMPLER(sampler_FreshRockNormalMap);
        TEXTURE2D(_SiltLayerMap);
        SAMPLER(sampler_SiltLayerMap);
        TEXTURE2D(_CavityNoiseRamp);
        SAMPLER(sampler_CavityNoiseRamp);
        TEXTURE2D(_GeologyStrataAlbedoMap);
        SAMPLER(sampler_GeologyStrataAlbedoMap);
        TEXTURE2D(_GeologyStrataMraoMap);
        SAMPLER(sampler_GeologyStrataMraoMap);
        TEXTURE3D(_BiomeFamilyTintVolume);
        SAMPLER(sampler_BiomeFamilyTintVolume);
        TEXTURE2D(_SargassumCutMaskRT);
        SAMPLER(sampler_SargassumCutMaskRT);
        TEXTURE3D(_HectonDamageVolumeTex);
        SAMPLER(sampler_HectonDamageVolumeTex);

        struct Attributes
        {
            HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float4 bakedAmbientOcclusion : TEXCOORD1;
            float4 dirtyBlendUv2 : TEXCOORD2;
            float4 absolutePositionWS : TEXCOORD3;
        };

        struct SurfaceVaryings
        {
            HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half3 viewDirWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            half skirtAlpha : TEXCOORD4;
            float3 absolutePositionWS : TEXCOORD5;
            half curvature : TEXCOORD6;
            half bakedAmbientOcclusion : TEXCOORD7;
            half freshCutBlend : TEXCOORD8;
            half4 terrainSplatColor : TEXCOORD9;
            half xrNearClipFade : TEXCOORD10;
            float2 xrFoveatedVector : TEXCOORD11;
        };

        struct ClipVaryings
        {
            HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half skirtAlpha : TEXCOORD1;
            half xrNearClipFade : TEXCOORD2;
        };

        struct ShadowVaryings
        {
            HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half skirtAlpha : TEXCOORD2;
        };

        float HectonVoxelRockFiniteOr(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float3 HectonVoxelRockFiniteOr(float3 value, float3 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        float4 HectonVoxelRockFiniteOr(float4 value, float4 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        half4 HectonVoxelRockFiniteOr(half4 value, half4 fallbackValue)
        {
            return all(isfinite(value)) ? value : fallbackValue;
        }

        half HectonVoxelRockFiniteSaturate(float value, half fallbackValue)
        {
            return (half)saturate(HectonVoxelRockFiniteOr(value, fallbackValue));
        }

        float HectonVoxelRockSafeTime()
        {
            return HectonVoxelRockFiniteOr(_Time.y, 0.0);
        }

        half3 SafeNormalize3(half3 value)
        {
            value = all(isfinite(value)) ? value : half3(0.0h, 1.0h, 0.0h);
            half lenSq = dot(value, value);
            half valid = isfinite(lenSq) ? step(0.0001h, lenSq) : 0.0h;
            half3 axis = abs(value);
            half maxAxis = max(axis.x, max(axis.y, axis.z));
            half minAxis = min(axis.x, min(axis.y, axis.z));
            half midAxis = axis.x + axis.y + axis.z - maxAxis - minAxis;
            half3 approximate = value * rcp(max(maxAxis + midAxis * 0.375h + minAxis * 0.25h, 0.0001h));
            return lerp(half3(0.0h, 1.0h, 0.0h), approximate, valid);
        }

        half FastVoxelPower01(half value, half exponent)
        {
            return (half)HectonCoreLitFastPower01((float)value, (float)exponent);
        }

        half3 FastVoxelPower01(half3 value, half exponent)
        {
            return half3(
                FastVoxelPower01(value.x, exponent),
                FastVoxelPower01(value.y, exponent),
                FastVoxelPower01(value.z, exponent));
        }

        half HectonVoxelSpecularLobe(half specularBase, half smoothness)
        {
            half b2 = specularBase * specularBase;
            half b4 = b2 * b2;
            half b8 = b4 * b4;
            half b16 = b8 * b8;
            half b32 = b16 * b16;
            half b64 = b32 * b32;
            half b96 = b64 * b32;
            return lerp(b16, b96, saturate(smoothness));
        }

        float4 ApplySkirtDepthBias(float4 positionCS, half skirtAlpha)
        {
            float skirtMask = saturate(skirtAlpha);
            float gradientBias = skirtMask * skirtMask;
            float clipBias = gradientBias * _SkirtDepthBias * max(positionCS.w, 0.0001);
            #if UNITY_REVERSED_Z
            positionCS.z += clipBias;
            #else
            positionCS.z -= clipBias;
            #endif
            return positionCS;
        }

        float Hash21(float2 value)
        {
            return HectonCoreLitHash12(value);
        }

        float ResolveDitherNoise(float2 positionCS)
        {
            positionCS = all(isfinite(positionCS)) ? positionCS : float2(0.0, 0.0);
            float2 pixel = floor(positionCS);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        float3 ResolveSamplePositionWS(float3 positionWS)
        {
            float3 safePositionWS = HectonVoxelRockFiniteOr(positionWS, float3(0.0, 0.0, 0.0));
            float3 safeOffset = HectonVoxelRockFiniteOr(_HectonFloatingOriginOffset.xyz, float3(0.0, 0.0, 0.0));
            return safePositionWS + safeOffset;
        }

        half3 ResolveChunkBorderStitchedNormal(half3 normalWS, float currentAbsoluteHeight, float neighborBorderHeight, half edgeBlend)
        {
            half heightAgreement = saturate(1.0h - (half)abs(neighborBorderHeight - currentAbsoluteHeight) * 0.5h);
            half stitch = saturate(edgeBlend * heightAgreement);
            half3 neighborNormalWS = SafeNormalize3(lerp(normalWS, half3(0.0h, 1.0h, 0.0h), 0.35h));
            return SafeNormalize3(lerp(normalWS, neighborNormalWS, stitch));
        }

        float ResolveCaveMouthVertexDisplacement(float3 absolutePositionWS, float3 normalOS, half splatBlend, half skirtAlpha)
        {
            float seamMask = saturate(max(splatBlend, skirtAlpha));
            if (seamMask <= 0.0001)
                return 0.0;

            float scale = max(_CaveMouthDisplacementScale, 0.05);
            float lowNoise = HectonCoreLitTrianglePulse01(dot(absolutePositionWS * scale + 17.13, float3(0.173, 0.097, 0.131)));
            float highNoise = HectonCoreLitTrianglePulse01(dot(absolutePositionWS * (scale * 2.37) + 41.9, float3(0.071, 0.149, 0.109)));
            float jagged = ((lowNoise * 0.72 + highNoise * 0.28) * 2.0) - 1.0;
            return jagged * _CaveMouthDisplacementStrength * seamMask;
        }

        float ResolveOrganicVertexDisplacement(float3 absolutePositionWS, float3 normalOS, half seamMask)
        {
            float scale = max(_OrganicDisplacementScale, 0.02);
            float lowNoise = HectonCoreLitTrianglePulse01(dot(absolutePositionWS * scale + 3.17, float3(0.157, 0.083, 0.119)));
            float midNoise = HectonCoreLitTrianglePulse01(dot(absolutePositionWS * (scale * 2.03) + 19.71, float3(0.097, 0.163, 0.071)));
            float fineNoise = HectonCoreLitTrianglePulse01(dot(absolutePositionWS * max(_OrganicDisplacementFineScale, 0.05) + 53.2, float3(0.193, 0.061, 0.137)));
            float ridged = abs(((lowNoise * 0.54 + midNoise * 0.31 + fineNoise * 0.15) * 2.0) - 1.0);
            float signedRidge = (ridged * 2.0) - 1.0;
            float slopeGate = saturate(1.0 - abs(normalOS.y) * 0.28);
            float seamBoost = 1.0 + saturate(seamMask) * _OrganicDisplacementSeamBoost;
            return signedRidge * _OrganicDisplacementStrength * seamBoost * slopeGate;
        }

        float3 ResolveVoxelPositionOS(Attributes input)
        {
            float3 safePositionOS = HectonVoxelRockFiniteOr(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
            float3 safeAbsolutePositionWS = HectonVoxelRockFiniteOr(input.absolutePositionWS.xyz, safePositionOS);
            float3 safeNormalOS = HectonCoreLitSafeNormalize(HectonVoxelRockFiniteOr(input.normalOS, float3(0.0, 1.0, 0.0)) + float3(0.0, 0.0001, 0.0));
            half seamMask = HectonVoxelRockFiniteSaturate(input.dirtyBlendUv2.y, 0.0h);
            float displacement = ResolveOrganicVertexDisplacement(safeAbsolutePositionWS, safeNormalOS, seamMask);
            displacement += ResolveCaveMouthVertexDisplacement(
                safeAbsolutePositionWS,
                safeNormalOS,
                0.0h,
                HectonVoxelRockFiniteSaturate(input.dirtyBlendUv2.y, 0.0h));
            displacement = HectonVoxelRockFiniteOr(displacement, 0.0);
            return safePositionOS + safeNormalOS * displacement;
        }

        void ApplyChunkDissolveFade(float4 positionCS)
        {
            half fade = saturate((half)_ChunkDissolveFade);
            if (fade >= 0.999h)
                return;

            clip(fade - ResolveDitherNoise(positionCS.xy));
        }

        half ApplyChunkDissolveMalfunction(float4 positionCS, float3 positionWS, inout half3 albedo, inout half smoothness)
        {
            half reveal = saturate(1.0h - (half)_ChunkDissolveFade);
            half strength = saturate((half)_ChunkDissolveGlitchStrength);
            if (reveal <= 0.0001h || strength <= 0.0001h)
                return 0.0h;

            float safeTime = HectonVoxelRockSafeTime();
            positionWS = HectonVoxelRockFiniteOr(positionWS, float3(0.0, 0.0, 0.0));
            float scanline = frac(HectonVoxelRockFiniteOr(positionCS.y, 0.0) * 0.125 + safeTime * 17.0);
            half scanPulse = smoothstep(0.84h, 0.98h, (half)scanline);
            half staticNoise = (half)Hash21(floor(positionWS.xz * 5.0 + safeTime * 3.0));
            half edgeNoise = smoothstep(0.52h, 0.93h, staticNoise);
            half malfunction = saturate((scanPulse * 0.55h + edgeNoise * 0.45h) * reveal * strength);

            albedo = lerp(albedo, (half3)_ChunkDissolvePhosphorTint.rgb, malfunction * 0.18h);
            smoothness = lerp(smoothness, 0.72h, malfunction * 0.5h);
            return malfunction;
        }

        half ResolveCaveMouthPhosphorPulse(float3 positionWS, half seamMask)
        {
            half gate = saturate(seamMask * (half)_CaveMouthPhosphorPulseStrength);
            if (gate <= 0.0001h)
                return 0.0h;

            float scale = max(_CaveMouthPhosphorPulseScale, 0.05);
            positionWS = HectonVoxelRockFiniteOr(positionWS, float3(0.0, 0.0, 0.0));
            float safeTime = HectonVoxelRockSafeTime();
            float phase = dot(positionWS, float3(0.37, 0.19, 0.41)) * scale + safeTime * 3.7;
            half sinePulse = smoothstep(0.78h, 1.0h, (half)HectonCoreLitTrianglePulse01(phase));
            half gridNoise = (half)HectonCoreLitTrianglePulse01(dot(positionWS.xz, float2(0.071, 0.149)) * (scale * 2.0) + safeTime);
            return gate * saturate(sinePulse * lerp(0.65h, 1.0h, gridNoise));
        }

        float2 ResolveAxisProjectionUv(float3 positionWS, half axis)
        {
            float tiling = max(_Tiling, 0.0001);
            if (axis < 0.5h)
                return positionWS.zy * tiling;

            if (axis > 1.5h)
                return positionWS.xy * tiling;

            return positionWS.xz * tiling;
        }

        void ResolveCinematicTwoAxisProjection(half3 normalWS, out half primaryAxis, out half secondaryAxis, out half secondaryWeight)
        {
            half3 axis = abs(normalWS);
            half primaryWeight;
            half candidateA;
            half candidateB;

            if (axis.x >= axis.y && axis.x >= axis.z)
            {
                primaryAxis = 0.0h;
                primaryWeight = axis.x;
                secondaryAxis = axis.z >= axis.y ? 2.0h : 1.0h;
                candidateA = axis.z;
                candidateB = axis.y;
            }
            else if (axis.z >= axis.y)
            {
                primaryAxis = 2.0h;
                primaryWeight = axis.z;
                secondaryAxis = axis.x >= axis.y ? 0.0h : 1.0h;
                candidateA = axis.x;
                candidateB = axis.y;
            }
            else
            {
                primaryAxis = 1.0h;
                primaryWeight = axis.y;
                secondaryAxis = axis.x >= axis.z ? 0.0h : 2.0h;
                candidateA = axis.x;
                candidateB = axis.z;
            }

            half secondaryMagnitude = max(candidateA, candidateB);
            secondaryWeight = saturate((secondaryMagnitude * rcp(max(primaryWeight + secondaryMagnitude, 0.0001h))) * 0.65h);
        }

        half4 SampleCinematicAxisColor(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS)
        {
            half primaryAxis;
            half secondaryAxis;
            half secondaryWeight;
            ResolveCinematicTwoAxisProjection(normalWS, primaryAxis, secondaryAxis, secondaryWeight);
            half4 primarySample = SAMPLE_TEXTURE2D(tex, samp, ResolveAxisProjectionUv(positionWS, primaryAxis));
            half4 secondarySample = SAMPLE_TEXTURE2D(tex, samp, ResolveAxisProjectionUv(positionWS, secondaryAxis));
            return lerp(primarySample, secondarySample, secondaryWeight);
        }

        float2 ResolveGeologyStrataProjectionUv(float3 positionWS, half axis)
        {
            float3 originAup = HectonVoxelRockFiniteOr(_GeologyWorldOriginAup.xyz, float3(0.0, 0.0, 0.0));
            float2 tileMeters = max(HectonVoxelRockFiniteOr(_GeologyTileMeters, float4(64.0, 95.0, 0.0, 0.0)).xy, float2(1.0, 1.0));
            float2 invTileMeters = rcp(tileMeters);
            if (axis < 0.5h)
                return float2(positionWS.z - originAup.z, positionWS.y - originAup.y) * invTileMeters;

            if (axis > 1.5h)
                return float2(positionWS.x - originAup.x, positionWS.y - originAup.y) * invTileMeters;

            float horizontalAup = (positionWS.x - originAup.x) + (positionWS.z - originAup.z) * 0.37;
            return float2(horizontalAup, positionWS.y - originAup.y) * invTileMeters;
        }

        half4 SampleGeologyBakedAxisColor(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS)
        {
            half primaryAxis;
            half secondaryAxis;
            half secondaryWeight;
            ResolveCinematicTwoAxisProjection(normalWS, primaryAxis, secondaryAxis, secondaryWeight);
            half4 primarySample = SAMPLE_TEXTURE2D(tex, samp, ResolveGeologyStrataProjectionUv(positionWS, primaryAxis));
            half4 secondarySample = SAMPLE_TEXTURE2D(tex, samp, ResolveGeologyStrataProjectionUv(positionWS, secondaryAxis));
            return lerp(primarySample, secondarySample, secondaryWeight);
        }

        half3 ResolveBiomeVisualFamilyTint(uint family)
        {
            if (family == 0u)
                return (half3)_BiomeFamilySandTint.rgb;
            if (family == 1u)
                return (half3)_BiomeFamilyBasaltTint.rgb;
            if (family == 2u)
                return (half3)_BiomeFamilyKelpTint.rgb;
            if (family == 3u)
                return (half3)_BiomeFamilyCoralTint.rgb;
            if (family == 4u)
                return (half3)_BiomeFamilyBrineTint.rgb;
            if (family == 5u)
                return (half3)_BiomeFamilyVolcanicTint.rgb;
            if (family == 7u)
                return (half3)_BiomeFamilyAlienTint.rgb;

            return (half3)_BiomeFamilyAbyssalTint.rgb;
        }

        int HectonVoxelRockRoundToIntFast(float value)
        {
            return value >= 0.0 ? (int)(value + 0.5) : (int)(value - 0.5);
        }

        half3 ResolveBiomeFamilyTintMultiplier(float3 absolutePositionWS)
        {
            if (_HectonScatterBiomeInfluenceGridCount <= 0 || _BiomeFamilyTintStrength <= 0.0001)
                return half3(1.0h, 1.0h, 1.0h);

            absolutePositionWS = HectonVoxelRockFiniteOr(absolutePositionWS, float3(0.0, 0.0, 0.0));
            float cellSize = max(HectonVoxelRockFiniteOr(_HectonScatterBiomeInfluenceGridParams.x, 1.0), 0.01);
            float invCellSize = rcp(cellSize);
            float3 safeGridOrigin = HectonVoxelRockFiniteOr(_HectonScatterBiomeInfluenceGridOrigin.xyz, float3(0.0, 0.0, 1.0));
            int gridSide = max(1, HectonVoxelRockRoundToIntFast(safeGridOrigin.z));
            int cellX = (int)floor(absolutePositionWS.x * invCellSize) - HectonVoxelRockRoundToIntFast(safeGridOrigin.x);
            int cellZ = (int)floor(absolutePositionWS.z * invCellSize) - HectonVoxelRockRoundToIntFast(safeGridOrigin.y);
            if (cellX < 0 || cellZ < 0 || cellX >= gridSide || cellZ >= gridSide)
                return half3(1.0h, 1.0h, 1.0h);

            int index = cellX + cellZ * gridSide;
            if (index < 0 || index >= _HectonScatterBiomeInfluenceGridCount)
                return half3(1.0h, 1.0h, 1.0h);

            uint packed = _HectonScatterBiomeInfluenceGrid[index];
            uint primaryFamily = packed & 7u;
            uint secondaryFamily = (packed >> 3) & 7u;
            half blend01 = (half)((packed >> 6) & 255u) * (1.0h / 255.0h);
            half3 primaryTint = ResolveBiomeVisualFamilyTint(primaryFamily);
            half3 secondaryTint = ResolveBiomeVisualFamilyTint(secondaryFamily);
            half3 familyTint = lerp(primaryTint, secondaryTint, blend01);
            return lerp(half3(1.0h, 1.0h, 1.0h), familyTint, saturate((half)_BiomeFamilyTintStrength));
        }

        half3 ResolveBiomeFamilyTintVolumeMultiplier(float3 absolutePositionWS)
        {
            if (_BiomeFamilyTintVolumeStrength <= 0.0001)
                return half3(1.0h, 1.0h, 1.0h);

            absolutePositionWS = HectonVoxelRockFiniteOr(absolutePositionWS, float3(0.0, 0.0, 0.0));
            float3 volumeOrigin = HectonVoxelRockFiniteOr(_BiomeFamilyTintVolumeWorldOrigin.xyz, float3(0.0, 0.0, 0.0));
            float3 volumeSize = max(HectonVoxelRockFiniteOr(_BiomeFamilyTintVolumeWorldSize.xyz, float3(1.0, 1.0, 1.0)), float3(1.0, 1.0, 1.0));
            float3 uvw = saturate((absolutePositionWS - volumeOrigin) * rcp(volumeSize));
            half3 volumeTint = SAMPLE_TEXTURE3D(_BiomeFamilyTintVolume, sampler_BiomeFamilyTintVolume, uvw).rgb;
            volumeTint = all(isfinite(volumeTint)) ? volumeTint : half3(1.0h, 1.0h, 1.0h);
            return lerp(half3(1.0h, 1.0h, 1.0h), volumeTint, saturate((half)_BiomeFamilyTintVolumeStrength));
        }

        half ResolveHorizontalSiltDust(half3 normalWS)
        {
            half upward = smoothstep(0.7h, 0.9h, saturate(normalWS.y));
            return saturate(upward * (half)_HorizontalSiltDustStrength);
        }

        half3 RecalculateDisplacedNormalWS(float3 positionWS, half3 fallbackNormalWS)
        {
            float3 dpdx = ddx(positionWS);
            float3 dpdy = ddy(positionWS);
            half3 derivedNormalWS = SafeNormalize3((half3)cross(dpdy, dpdx));
            return dot(derivedNormalWS, fallbackNormalWS) < 0.0h ? -derivedNormalWS : derivedNormalWS;
        }

        half3 ResolveScreenSpaceSmoothedVoxelNormal(half3 coarseNormalWS, float3 positionWS, half curvature)
        {
            if (_HectonMathLodMode < 0.5)
                return coarseNormalWS;

            float3 dpdx = ddx(positionWS);
            float3 dpdy = ddy(positionWS);
            half3 faceNormalWS = SafeNormalize3((half3)cross(dpdy, dpdx));
            faceNormalWS = dot(faceNormalWS, coarseNormalWS) < 0.0h ? -faceNormalWS : faceNormalWS;

            float3 pixelSpan = abs(dpdx) + abs(dpdy);
            half bevelMask = saturate((half)((pixelSpan.x + pixelSpan.y + pixelSpan.z) * max(_ScreenSpaceNormalBevelStrength, 0.0)));
            half cavityWeight = saturate((0.5h - curvature) * 2.0h);
            half organicWeight = lerp(0.35h, 0.82h, saturate(cavityWeight * 0.7h + bevelMask * 0.3h));
            half3 smoothedNormalWS = SafeNormalize3(lerp(coarseNormalWS, faceNormalWS, bevelMask * organicWeight));

            half3 tangentX = SafeNormalize3((half3)dpdx);
            half3 tangentY = SafeNormalize3((half3)dpdy);
            half deterministicFacetWeight = (half)_ScreenSpaceNormalNoiseStrength * (0.12h + cavityWeight * 0.18h);
            return SafeNormalize3(smoothedNormalWS + (tangentX + tangentY) * deterministicFacetWeight * bevelMask);
        }

        half ResolveDepthNoiseCavityAo(float3 absolutePositionWS, half bakedAmbientOcclusion)
        {
            if (_HectonMathLodMode < 0.5)
                return bakedAmbientOcclusion;

            absolutePositionWS = HectonVoxelRockFiniteOr(absolutePositionWS, float3(0.0, 0.0, 0.0));
            float depthU = frac(absolutePositionWS.y * max(_CavityAoDepthScale, 0.001));
            half rampNoise = SAMPLE_TEXTURE2D(_CavityNoiseRamp, sampler_CavityNoiseRamp, float2(depthU, 0.5)).r;
            half aoNoise = lerp(1.0h, lerp(0.72h, 1.08h, rampNoise), saturate((half)_CavityAoNoiseStrength));
            return saturate(bakedAmbientOcclusion * aoNoise);
        }

        half3 SampleDominantAxisNormalAtUv(float2 uv, half dominantAxis, half3 baseNormalWS)
        {
            half3 normalSign = sign(baseNormalWS);
            half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, uv));
            if (dominantAxis < 0.5h)
                return SafeNormalize3(half3(tangentNormal.z * normalSign.x, tangentNormal.y, tangentNormal.x));

            if (dominantAxis > 1.5h)
                return SafeNormalize3(half3(tangentNormal.x, tangentNormal.y, tangentNormal.z * normalSign.z));

            return SafeNormalize3(half3(tangentNormal.x, tangentNormal.z * normalSign.y, tangentNormal.y));
        }

        half3 SampleCinematicTwoAxisNormal(float3 positionWS, half3 baseNormalWS)
        {
            half primaryAxis;
            half secondaryAxis;
            half secondaryWeight;
            ResolveCinematicTwoAxisProjection(baseNormalWS, primaryAxis, secondaryAxis, secondaryWeight);
            half3 primaryNormalWS = SampleDominantAxisNormalAtUv(ResolveAxisProjectionUv(positionWS, primaryAxis), primaryAxis, baseNormalWS);
            half3 secondaryNormalWS = SampleDominantAxisNormalAtUv(ResolveAxisProjectionUv(positionWS, secondaryAxis), secondaryAxis, baseNormalWS);
            return lerp(primaryNormalWS, secondaryNormalWS, secondaryWeight);
        }

        half3 DecodeVoxelArrayNormal(half3 packedNormal)
        {
            half2 xy = packedNormal.rg * 2.0h - 1.0h;
            half z = sqrt(max(1.0e-4h, 1.0h - dot(xy, xy)));
            return SafeNormalize3(half3(xy, z));
        }

        float3 HectonSimplexMod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 HectonSimplexMod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 HectonSimplexPermute(float4 x) { return HectonSimplexMod289(((x * 34.0) + 1.0) * x); }
        float4 HectonSimplexTaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

        float HectonSimplexNoise3D(float3 v)
        {
            const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
            const float4 D = float4(0.0, 0.5, 1.0, 2.0);

            float3 i  = floor(v + dot(v, C.yyy));
            float3 x0 = v - i + dot(i, C.xxx);

            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min(g.xyz, l.zxy);
            float3 i2 = max(g.xyz, l.zxy);

            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - D.yyy;

            i = HectonSimplexMod289(i);
            float4 p = HectonSimplexPermute(HectonSimplexPermute(HectonSimplexPermute(
                        i.z + float4(0.0, i1.z, i2.z, 1.0))
                    + i.y + float4(0.0, i1.y, i2.y, 1.0))
                    + i.x + float4(0.0, i1.x, i2.x, 1.0));

            float n_ = 0.142857142857;
            float3 ns = n_ * D.wyz - D.xzx;

            float4 j = p - 49.0 * floor(p * ns.z);

            float4 x_ = floor(j * ns.z);
            float4 y_ = floor(j - 7.0 * x_);

            float4 x = x_ * ns.x + ns.yyyy;
            float4 y = y_ * ns.x + ns.yyyy;
            float4 h = 1.0 - abs(x) - abs(y);

            float4 b0 = float4(x.xy, y.xy);
            float4 b1 = float4(x.zw, y.zw);

            float4 s0 = floor(b0) * 2.0 + 1.0;
            float4 s1 = floor(b1) * 2.0 + 1.0;
            float4 sh = -step(h, float4(0.0, 0.0, 0.0, 0.0));

            float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
            float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

            float3 p0 = float3(a0.xy, h.x);
            float3 p1 = float3(a0.zw, h.y);
            float3 p2 = float3(a1.xy, h.z);
            float3 p3 = float3(a1.zw, h.w);

            float4 norm = HectonSimplexTaylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
            p0 *= norm.x;
            p1 *= norm.y;
            p2 *= norm.z;
            p3 *= norm.w;

            float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
            m = m * m;
            return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
        }

        half3 ResolveOrganicBioluminescenceEmission(float3 positionWS, half vertexCaveAo)
        {
            float n1 = HectonSimplexNoise3D(positionWS * 0.55);
            float n2 = HectonSimplexNoise3D(positionWS * 1.85);

            float ridgedVeins = 1.0 - abs(n1 * 0.7 + n2 * 0.3);
            ridgedVeins = smoothstep(0.92, 0.98, ridgedVeins); // TIGHT gate: only sharp zero-crossings glow as thin branching veins

            float creviceMask = saturate((1.0 - vertexCaveAo) * 2.5);
            float emissionMask = ridgedVeins * creviceMask;

            half3 cyanGlow = half3(0.02h, 0.78h, 0.98h);
            return cyanGlow * (emissionMask * 4.5h); // Brighter to compensate for tighter mask
        }

        half3 ResolveVoxelTriplanarWeights(half3 normalWS)
        {
            float3 blendWeights = abs((float3)normalWS);
            blendWeights = pow(blendWeights, 8.0); // HIGH EXPONENT: razor-sharp axis selection kills moiré interference
            float sum = blendWeights.x + blendWeights.y + blendWeights.z;
            return (half3)(blendWeights * rcp(max(sum, 0.0001)));
        }

        // Anti-tiling for the triplanar rock/sand arrays.
        //
        // The lattice repeats every 1/_VoxelTriplanarScale meters (12.5 m at the 0.08 default) and the
        // eye locks onto that as an axis-aligned grid. The obvious fix - a per-hex-cell random UV
        // offset - is WRONG in a triplanar fragment path and was actively harmful here:
        //   1. it is discontinuous at every cell border, so the texture tears along the whole lattice;
        //   2. those UV jumps make the implicit ddx/ddy used by SAMPLE_TEXTURE2D_ARRAY explode, so the
        //      hardware selects the coarsest mip on border pixels - a blurry seam grid at the CELL
        //      size (~3.9 m), i.e. a higher-frequency artifact than the repeat it was meant to hide;
        //   3. frac(sin(dot(cell, ...)) * 43758.5453) collapses at AUP magnitudes. World coordinates
        //      here run to the 6627 m cave wrap period, well past where that hash keeps precision.
        //
        // Instead this warps the sample domain with a C1 low-frequency field. It is continuous
        // everywhere, so derivatives stay finite and mip selection stays correct, and because the warp
        // is a spatially varying TRANSLATION it does not rotate the tangent frame that
        // VoxelArrayNormalToWorld assumes - albedo and normal stay in phase because both route through
        // this same pure function of (positionWS, axis).
        //
        // Wavelength is deliberately a few tiles (~60 m), not hundreds of meters: a very slow warp
        // leaves neighbouring repeats still locally aligned, which is exactly what reads as tiling.
        // Amplitude is held so |A * frequency| stays near 0.15, keeping the Jacobian close to identity
        // (texel density varies about +/-25% over 60 m) so the material does not visibly stretch.
        //
        // NOTE ON SCOPE: this curves the repeat lattice, it does not eliminate repetition. Removing it
        // outright needs hex-tile stochastic sampling with 3 barycentric-weighted taps and explicit
        // SAMPLE_..._GRAD derivatives, which triples the fetch count of this path. This shader already
        // issues 12 array fetches per pixel (sand+rock albedo and normal, 3 axes each), so that change
        // is a profiled budget decision, not a free win, and is deliberately not taken here.
        float2 ResolveVoxelAntiTileWarp(float2 uv, float strength)
        {
            const float2 primaryFrequency = float2(1.31, 1.07);
            const float2 crossFrequency = float2(0.50, 0.60);
            const float baseAmplitude = 0.11;

            float2 warp = float2(
                sin(uv.x * primaryFrequency.x + uv.y * crossFrequency.x),
                sin(uv.y * primaryFrequency.y - uv.x * crossFrequency.y));
            return uv + warp * (baseAmplitude * saturate(strength));
        }

        float2 ResolveVoxelTriplanarUv(float3 positionWS, int axis)
        {
            float scale = max(_VoxelTriplanarScale, 0.0001);
            float2 uv;
            if (axis == 0)
                uv = positionWS.zy * scale;
            else if (axis == 2)
                uv = positionWS.xy * scale;
            else
                uv = positionWS.xz * scale;

            // Uniform (per-material) branch: costs nothing at runtime and lets the effect be disabled
            // without a keyword permutation.
            if (_VoxelStochasticStrength > 0.001)
                uv = ResolveVoxelAntiTileWarp(uv, _VoxelStochasticStrength);

            return uv;
        }

        half3 VoxelArrayNormalToWorld(half3 tangentNormal, int axis, half3 baseNormalWS)
        {
            half3 normalSign = sign(baseNormalWS);
            if (axis == 0)
                return SafeNormalize3(half3(tangentNormal.z * normalSign.x, tangentNormal.y, tangentNormal.x));
            if (axis == 2)
                return SafeNormalize3(half3(tangentNormal.x, tangentNormal.y, tangentNormal.z * normalSign.z));
            return SafeNormalize3(half3(tangentNormal.x, tangentNormal.z * normalSign.y, tangentNormal.y));
        }

        half3 SampleVoxelArrayAlbedo(float3 positionWS, half3 weights, float layerIndex)
        {
            half3 sx = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, ResolveVoxelTriplanarUv(positionWS, 0), layerIndex).rgb;
            half3 sy = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, ResolveVoxelTriplanarUv(positionWS, 1), layerIndex).rgb;
            half3 sz = SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, ResolveVoxelTriplanarUv(positionWS, 2), layerIndex).rgb;
            return sx * weights.x + sy * weights.y + sz * weights.z;
        }

        half3 SampleVoxelArrayNormalWS(float3 positionWS, half3 baseNormalWS, half3 weights, float layerIndex)
        {
            half3 tx = DecodeVoxelArrayNormal(SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, ResolveVoxelTriplanarUv(positionWS, 0), layerIndex).rgb);
            half3 ty = DecodeVoxelArrayNormal(SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, ResolveVoxelTriplanarUv(positionWS, 1), layerIndex).rgb);
            half3 tz = DecodeVoxelArrayNormal(SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, ResolveVoxelTriplanarUv(positionWS, 2), layerIndex).rgb);
            half3 nx = VoxelArrayNormalToWorld(tx, 0, baseNormalWS);
            half3 ny = VoxelArrayNormalToWorld(ty, 1, baseNormalWS);
            half3 nz = VoxelArrayNormalToWorld(tz, 2, baseNormalWS);
            return SafeNormalize3(nx * weights.x + ny * weights.y + nz * weights.z);
        }

        half EvaluateGlobalCutMask(float3 positionWS)
        {
            if (_SargassumCutMaskActive < 0.5 || !all(isfinite(positionWS)) || !all(isfinite(_SargassumCutMaskWorldRect)))
                return 0.0h;

            float2 uv = float2(
                (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
            if (!all(isfinite(uv)) || uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                return 0.0h;

            return HectonVoxelRockFiniteSaturate(SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r, 0.0h);
        }

        half EvaluateDamageVolumeMask(float3 positionWS)
        {
            if (_HectonDamageVolumeActive < 0.5 || !all(isfinite(positionWS)) || !all(isfinite(_HectonDamageVolumeWorldMin.xyz)) || !all(isfinite(_HectonDamageVolumeInvSize.xyz)))
                return 0.0h;

            float3 uvw = (positionWS - _HectonDamageVolumeWorldMin.xyz) * _HectonDamageVolumeInvSize.xyz;
            if (!all(isfinite(uvw)) || uvw.x < 0.0 || uvw.x > 1.0 || uvw.y < 0.0 || uvw.y > 1.0 || uvw.z < 0.0 || uvw.z > 1.0)
                return 0.0h;

            return HectonVoxelRockFiniteSaturate(SAMPLE_TEXTURE3D_LOD(_HectonDamageVolumeTex, sampler_HectonDamageVolumeTex, uvw, 0).r, 0.0h);
        }

        half ResolveDearLieCarveMask(half globalCutMask, half damageVolumeMask)
        {
            return saturate(max(globalCutMask, damageVolumeMask));
        }

        void ApplyDearLieCarveClip(half carveMask, float2 positionCS)
        {
            half clipStrength = saturate((carveMask - 0.45h) * 1.8181818h);
            half coverage = saturate(1.0h - clipStrength);
            clip(coverage - ResolveDitherNoise(positionCS) * 0.125h);
        }

        half ResolveSkirtCoverageMask(half vertexAlpha)
        {
            half bandMeters = max((half)_TerrainSeamBandMeters, 0.01h);
            half fadeMeters = clamp((half)_TerrainSeamFadeDistance, 0.01h, bandMeters);
            half fadeStartAlpha = saturate(1.0h - fadeMeters * rcp(bandMeters));
            half remappedAlpha = saturate((saturate(vertexAlpha) - fadeStartAlpha) * rcp(max(1.0h - fadeStartAlpha, 0.0001h)));
            return FastVoxelPower01(remappedAlpha, max(_SkirtBlendContrast, 0.1));
        }

        half ResolveSkirtCoverage(half vertexAlpha, float2 positionCS)
        {
            half shapedAlpha = ResolveSkirtCoverageMask(vertexAlpha);
            clip(shapedAlpha - ResolveDitherNoise(positionCS));
            return shapedAlpha;
        }

        half ResolveShadowCoverage(half vertexAlpha, float2 positionCS, half scarMask, float3 positionWS)
        {
            half shapedAlpha = ResolveSkirtCoverageMask(vertexAlpha);
            float scarNoise = Hash21(floor(positionWS.xz * 3.0 + scarMask * 19.0));
            half erosion = scarMask * _ShadowScarErosion * (1.0h - scarNoise);
            half coverage = saturate(shapedAlpha - erosion);
            clip(coverage - ResolveDitherNoise(positionCS));
            return coverage;
        }

        half ResolveNoirVoxelCaustic(float3 absolutePositionWS, half3 normalWS, float4 positionCS)
        {
            absolutePositionWS = HectonVoxelRockFiniteOr(absolutePositionWS, float3(0.0, 0.0, 0.0));
            float safeWaterline = HectonVoxelRockFiniteOr(_HectonNoirFogStratification.x, 0.0);
            float waterDepth = max(0.0, safeWaterline - absolutePositionWS.y);
            float4 safeShape = HectonVoxelRockFiniteOr(_HectonNoirCausticsShape, float4(1.0h, 0.0h, 0.0h, 1.0h));
            float4 safeLayerA = HectonVoxelRockFiniteOr(_HectonNoirCausticsLayerA, float4(0.02h, 0.0h, 0.0h, 0.0h));
            float4 safeLayerB = HectonVoxelRockFiniteOr(_HectonNoirCausticsLayerB, float4(0.02h, 0.0h, 0.0h, 0.0h));
            float depthFade = 1.0 - saturate((waterDepth - safeShape.z) * rcp(max(safeShape.w, 0.25)));
            if (depthFade <= 0.0)
                return 1.0h;

            half upFacingMask = (half)HectonCoreLitEvaluateCausticsUpMask(normalWS);
            half strength = saturate((safeLayerA.w + safeLayerB.w) * depthFade);
            if (upFacingMask <= 0.0001h || strength <= 0.0001h)
                return 1.0h;

            float timePhase = HectonVoxelRockSafeTime();
            float3 layerABasis = absolutePositionWS * max(safeLayerA.x, 0.02);
            float3 layerBSampleAnchor = absolutePositionWS * max(safeLayerB.x, 0.02);
            float3 layerAInput = float3(
                layerABasis.x + timePhase * safeLayerA.y,
                layerABasis.y * 0.23 + timePhase * (safeLayerA.y * 0.31 + safeLayerA.z * 0.17),
                layerABasis.z + timePhase * safeLayerA.z);
            float distortion = (HectonCoreLitTrianglePulse01(dot(layerAInput, float3(0.173, 0.097, 0.131)) + 7.1) * 2.0 - 1.0) * safeShape.y;
            float3 layerBInput = float3(
                layerBSampleAnchor.x + timePhase * safeLayerB.y + distortion,
                layerBSampleAnchor.y * 0.19 + timePhase * (safeLayerB.y * 0.27 + safeLayerB.z * 0.23),
                layerBSampleAnchor.z + timePhase * safeLayerB.z - distortion);
            half layerA = (half)HectonCoreLitTrianglePulse01(dot(layerAInput, float3(0.071, 0.149, 0.109)));
            half layerB = (half)HectonCoreLitTrianglePulse01(dot(layerBInput, float3(0.113, 0.083, 0.167)));
            half causticRaw = FastVoxelPower01(saturate(layerA * layerB), (half)max(safeShape.x, 1.0));
            half caveFade = 1.0h;
            return lerp(1.0h, 1.0h + causticRaw * strength * caveFade, upFacingMask);
        }

        half ResolveLocalLightCaustic(float3 absolutePositionWS, half3 normalWS, float4 positionCS)
        {
            if (_HectonNoirResolveSettings.z > 0.5h)
                return ResolveNoirVoxelCaustic(absolutePositionWS, normalWS, positionCS);

            half localStrength = saturate(_LocalCausticStrength);
            if (localStrength <= 0.0001h)
                return 1.0h;

            half upFacingMask = (half)saturate(HectonCoreLitEvaluateCausticsUpMask(normalWS) * 0.72 + 0.1);
            half causticMask = saturate(localStrength * upFacingMask);
            if (causticMask <= 0.0001h)
                return 1.0h;

            absolutePositionWS = HectonVoxelRockFiniteOr(absolutePositionWS, float3(0.0, 0.0, 0.0));
            float scale = max(HectonVoxelRockFiniteOr(_LocalCausticScale, 0.05), 0.05);
            float speed = max(HectonVoxelRockFiniteOr(_LocalCausticSpeed, 0.0), 0.0);
            float timePhase = HectonVoxelRockSafeTime() * speed;
            float3 causticBasis = absolutePositionWS * scale;
            float3 layerASample = float3(
                causticBasis.x + timePhase * 0.83,
                causticBasis.y * 0.27 + timePhase * 0.19,
                causticBasis.z + timePhase * 0.56);
            float3 layerBSample = float3(
                causticBasis.x * 1.37 - timePhase * 0.41 + 13.1,
                causticBasis.y * 0.19 + timePhase * 0.23 + 7.3,
                causticBasis.z * 1.37 + timePhase * 0.91 + 5.7);
            half layerA = (half)HectonCoreLitTrianglePulse01(dot(layerASample, float3(0.137, 0.061, 0.193)));
            half layerB = (half)HectonCoreLitTrianglePulse01(dot(layerBSample, float3(0.097, 0.157, 0.071)));
            half causticSeed = saturate(layerA * layerB);
            half causticRaw = causticSeed * causticSeed * causticSeed;
            return lerp(1.0h, 1.0h + causticRaw * localStrength, causticMask);
        }

        half3 EvaluateLighting(float3 positionWS, float4 positionCS, half3 normalWS, half3 viewDirWS, half3 albedo, half metallic, half smoothness, half occlusion, half localCausticMask)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = H8CustomLightProbeResolveAmbient(positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * albedo * occlusion * caveAmbientFactor;
            half specularStrength = lerp(0.04h, 0.22h, metallic);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = SafeNormalize3(mainLight.direction);
            half NdotL = saturate(dot(normalWS, lightDir));
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (NdotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                if (specularBase > 0.0001h)
                    specular = HectonVoxelSpecularLobe(specularBase, smoothness) * specularEnergy;
            }
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            color += (albedo * NdotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow);

            #if defined(_ADDITIONAL_LIGHTS)
            half additionalLightWeight = (half)smoothstep(0.2, 0.75, HectonCoreLitMathLodWeight());
            if (additionalLightWeight > 0.0001h)
            {
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    half3 additionalDir = SafeNormalize3(light.direction);
                    half additionalNdotL = saturate(dot(normalWS, additionalDir));
                    half additionalSpecular = 0.0h;
                    if (additionalNdotL > 0.0001h && specularEnergy > 0.0001h)
                    {
                        half3 additionalHalfDir = SafeNormalize3(additionalDir + viewDirWS);
                        half additionalSpecularBase = saturate(dot(normalWS, additionalHalfDir));
                        if (additionalSpecularBase > 0.0001h)
                            additionalSpecular = HectonVoxelSpecularLobe(additionalSpecularBase, smoothness) * specularEnergy;
                    }
                    half causticLightMask = lerp(1.0h, localCausticMask, saturate(additionalNdotL * light.distanceAttenuation));
                    float additionalShadowAttenuation = HectonCoreLitResolveFlashlightAdditionalShadow(lightIndex, positionWS, normalWS, light.shadowAttenuation);
                    color += ((albedo * additionalNdotL + additionalSpecular) * causticLightMask) * light.color * (light.distanceAttenuation * additionalShadowAttenuation * additionalLightWeight);
                LIGHT_LOOP_END
            }
            #endif

            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;

            return color;
        }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        SurfaceVaryings Vert(Attributes input)
        {
            SurfaceVaryings output;
            HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
            HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
            HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            float3 safeDisplacedPositionOS = HectonCoreLitSanitizePositionOS(displacedPositionOS);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(safeDisplacedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.normalWS = ResolveChunkBorderStitchedNormal(
                SafeNormalize3(normalInputs.normalWS),
                input.absolutePositionWS.y,
                input.absolutePositionWS.w,
                saturate((half)input.dirtyBlendUv2.w));
            output.positionWS = HectonCoreLitApplySubmarineCrushDepth(positionInputs.positionWS, output.normalWS);
            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(output.positionWS, output.normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(output.positionWS));
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.absolutePositionWS = input.absolutePositionWS.xyz;
            output.curvature = saturate(input.dirtyBlendUv2.z);
            output.bakedAmbientOcclusion = HectonCoreLitResolveVertexAmbientOcclusion(input.bakedAmbientOcclusion.w);
            output.freshCutBlend = saturate(input.dirtyBlendUv2.x);
            output.terrainSplatColor = saturate((half4)input.color);
            output.positionCS = ApplySkirtDepthBias(output.positionCS, output.skirtAlpha);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.xrNearClipFade = (half)HectonCoreLitEvaluateXRNearClipFade(output.positionWS);
            output.xrFoveatedVector = HectonCoreLitBuildStereoFoveationVector(output.positionWS);
            return output;
        }

        ClipVaryings DepthVert(Attributes input)
        {
            ClipVaryings output;
            HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
            HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
            HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            float3 safeDisplacedPositionOS = HectonCoreLitSanitizePositionOS(displacedPositionOS);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(safeDisplacedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            float3 normalWS = ResolveChunkBorderStitchedNormal(
                SafeNormalize3(normalInputs.normalWS),
                input.absolutePositionWS.y,
                input.absolutePositionWS.w,
                saturate((half)input.dirtyBlendUv2.w));
            float3 crushedPositionWS = HectonCoreLitApplySubmarineCrushDepth(positionInputs.positionWS, normalWS);
            crushedPositionWS = HectonCoreLitApplyStormRainDripVertexRipple(crushedPositionWS, normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.positionCS = ApplySkirtDepthBias(TransformWorldToHClip(crushedPositionWS), output.skirtAlpha);
            output.positionWS = crushedPositionWS;
            output.xrNearClipFade = (half)HectonCoreLitEvaluateXRNearClipFade(output.positionWS);
            return output;
        }

        ShadowVaryings ShadowVert(Attributes input)
        {
            ShadowVaryings output;
            HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
            HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
            HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 displacedPositionOS = ResolveVoxelPositionOS(input);
            float3 safeDisplacedPositionOS = HectonCoreLitSanitizePositionOS(displacedPositionOS);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(safeDisplacedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.normalWS = ResolveChunkBorderStitchedNormal(
                SafeNormalize3(normalInputs.normalWS),
                input.absolutePositionWS.y,
                input.absolutePositionWS.w,
                saturate((half)input.dirtyBlendUv2.w));
            output.positionWS = HectonCoreLitApplySubmarineCrushDepth(positionInputs.positionWS, output.normalWS);
            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(output.positionWS, output.normalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.skirtAlpha = saturate(input.dirtyBlendUv2.y);
            output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, _LightDirection));
            output.positionCS = ApplySkirtDepthBias(output.positionCS, output.skirtAlpha);

            #if UNITY_REVERSED_Z
            output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
            output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            AlphaToMask On
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            half4 Frag(SurfaceVaryings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HectonCoreLitClipXRNearWallDither(input.xrNearClipFade, input.positionCS);
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                bool xrFullQuality = HectonCoreLitShouldRunXRFullQuality(input.xrFoveatedVector);
                half skirtCoverage = ResolveSkirtCoverageMask(input.skirtAlpha);
                float3 samplePositionWS = input.absolutePositionWS;
                half3 coarseNormalWS = SafeNormalize3(input.normalWS);
                half3 baseNormalWS = ResolveScreenSpaceSmoothedVoxelNormal(coarseNormalWS, input.positionWS, input.curvature);
                half4 color = input.terrainSplatColor;
                half2 materialWeights = max(color.rg, half2(0.0h, 0.0h));
                half materialWeightSum = materialWeights.x + materialWeights.y;
                materialWeights = materialWeightSum > 0.0001h ? materialWeights * rcp(materialWeightSum) : half2(0.0h, 1.0h);
                half vertexCaveAo = saturate(color.r * max(color.a > 0.001h ? color.a : 1.0h, input.bakedAmbientOcclusion));
                half noisyBakedAo = ResolveDepthNoiseCavityAo(samplePositionWS, vertexCaveAo);
                half3 dominantNormalWS = SampleCinematicTwoAxisNormal(samplePositionWS, baseNormalWS);
                half globalCutMask = EvaluateGlobalCutMask(input.positionWS);
                half damageVolumeMask = globalCutMask >= 0.999h ? 0.0h : EvaluateDamageVolumeMask(input.positionWS);
                half cutMask = ResolveDearLieCarveMask(globalCutMask, damageVolumeMask);
                ApplyDearLieCarveClip(cutMask, input.positionCS.xy);

                half4 baseSample = SampleCinematicAxisColor(TEXTURE2D_ARGS(_Base_Map, sampler_Base_Map), samplePositionWS, baseNormalWS);
                half3 triplanarWeights = ResolveVoxelTriplanarWeights(baseNormalWS);
                half3 sandAlbedo = SampleVoxelArrayAlbedo(samplePositionWS, triplanarWeights, _VoxelSandArrayIndex);
                half3 rockAlbedo = SampleVoxelArrayAlbedo(samplePositionWS, triplanarWeights, _VoxelRockArrayIndex);
                half sampledArrayValid = step(0.006h, dot(sandAlbedo + rockAlbedo, half3(0.2126h, 0.7152h, 0.0722h)));
                baseSample.rgb = lerp(baseSample.rgb, lerp(sandAlbedo, rockAlbedo, materialWeights.y), sampledArrayValid);
                half4 packedMask = SampleCinematicAxisColor(TEXTURE2D_ARGS(_Mask_Map, sampler_Mask_Map), samplePositionWS, baseNormalWS);
                HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
                half geologyBlend = saturate((half)_GeologyStrataBlend);
                half bakedOreMetallic = 0.0h;
                half bakedSedimentMask = 0.0h;
                [branch]
                if (geologyBlend > 0.0001h)
                {
                    half4 geologyAlbedoSample = SampleGeologyBakedAxisColor(TEXTURE2D_ARGS(_GeologyStrataAlbedoMap, sampler_GeologyStrataAlbedoMap), samplePositionWS, baseNormalWS);
                    half4 geologyMraoSample = SampleGeologyBakedAxisColor(TEXTURE2D_ARGS(_GeologyStrataMraoMap, sampler_GeologyStrataMraoMap), samplePositionWS, baseNormalWS);
                    half bakedRoughness = saturate(geologyMraoSample.g);
                    half bakedAmbientOcclusion = saturate(geologyMraoSample.b);
                    bakedOreMetallic = saturate(geologyMraoSample.r);
                    bakedSedimentMask = saturate(geologyMraoSample.a * geologyBlend);
                    baseSample.rgb = lerp(baseSample.rgb, geologyAlbedoSample.rgb, geologyBlend);
                    decodedMask.metallic = lerp(decodedMask.metallic, max(decodedMask.metallic, bakedOreMetallic), geologyBlend);
                    decodedMask.smoothness = lerp(decodedMask.smoothness, 1.0h - bakedRoughness, geologyBlend);
                    decodedMask.occlusion = lerp(decodedMask.occlusion, min(decodedMask.occlusion, bakedAmbientOcclusion), geologyBlend);
                }
                half freshCutMask = saturate(max(input.freshCutBlend, cutMask));
                half scarMask = FastVoxelPower01(saturate(cutMask), max(_CutScarSharpness, 0.5h));
                half recentHeatMask = 0.0h;
                half recentHeatAge01 = 1.0h;
                half3 sandNormalWS = SampleVoxelArrayNormalWS(samplePositionWS, baseNormalWS, triplanarWeights, _VoxelSandArrayIndex);
                half3 rockNormalWS = SampleVoxelArrayNormalWS(samplePositionWS, baseNormalWS, triplanarWeights, _VoxelRockArrayIndex);
                half3 triplanarNormalWS = SafeNormalize3(lerp(sandNormalWS, rockNormalWS, materialWeights.y));
                half3 boostedNormalWS = lerp(dominantNormalWS, triplanarNormalWS, saturate((half)_VoxelArrayNormalStrength * sampledArrayValid));
                boostedNormalWS *= lerp(1.0h, (half)_FreshCutNormalBoost, freshCutMask * 0.5h);
                half3 normalWS = SafeNormalize3(baseNormalWS + boostedNormalWS);
                normalWS = HectonCoreLitApplyTripleDetailMicroNormals(input.positionWS, normalWS, (half)_MicroNormalStrength, (half)_MicroNormalTiling, 2.0h);
                half skirtBlend = 1.0h - skirtCoverage;
                half curvature = saturate(input.curvature);
                half curvatureContrast = max(_CurvatureContrast, 0.5h);
                half convexMask = FastVoxelPower01(saturate((curvature - 0.5h) * 2.0h), curvatureContrast);
                half cavityMask = FastVoxelPower01(saturate((0.5h - curvature) * 2.0h), curvatureContrast);

                half3 albedo = baseSample.rgb * _Instance_Color.rgb;
                albedo *= ResolveBiomeFamilyTintMultiplier(samplePositionWS);
                albedo *= ResolveBiomeFamilyTintVolumeMultiplier(samplePositionWS);
                half3 freshAlbedo = baseSample.rgb * _Instance_Color.rgb * (half)_FreshCutColorBoost;
                freshAlbedo = lerp(freshAlbedo, (half3)_CutScarColor.rgb, scarMask * 0.45h);
                albedo = lerp(albedo, freshAlbedo, freshCutMask * 0.62h);
                albedo = lerp(albedo, _SkirtSandTint.rgb, skirtBlend * 0.72h);
                albedo = lerp(albedo, lerp(albedo, _CurvatureWearTint.rgb, 0.4h), convexMask * _CurvatureEdgeWearStrength);
                albedo *= 1.0h - cavityMask * (_CurvatureCavityDarkenStrength * 0.32h);
                albedo *= lerp(1.0h, 1.0h - _CutScarDarkening, scarMask);
                albedo = lerp(albedo, _CutScarCharColor.rgb, scarMask * 0.38h);

                half3 thermalColor = lerp(_CutScarWarmColor.rgb, _CutScarColor.rgb, saturate(1.0h - recentHeatAge01 * 0.9h));
                albedo = lerp(albedo, thermalColor, recentHeatMask * 0.18h);


                half metallic = decodedMask.metallic;
                half smoothness = saturate(lerp(decodedMask.smoothness, 0.88h, scarMask * 0.65h) + convexMask * (_CurvatureEdgeWearStrength * 0.08h));
                smoothness = saturate(smoothness + bakedOreMetallic * geologyBlend * (half)_GeologyOreGlintStrength * 0.12h);
                // PATCHY WETNESS: modulate smoothness with low-frequency 3D Simplex noise
                // Prevents uniform plastic wrap — creates wet puddle patches (0.82) vs dry matte rock (0.12)
                float wetNoise = HectonSimplexNoise3D(samplePositionWS * 0.2);
                half wetMask = (half)smoothstep(0.3, 0.7, wetNoise * 0.5 + 0.5);
                smoothness = lerp(0.12h, 0.82h, wetMask);
                half ambientOcclusion = saturate(noisyBakedAo * vertexCaveAo * decodedMask.occlusion * (1.0h - cavityMask * _CurvatureCavityDarkenStrength));
                half caveMouthDistanceAo = saturate(input.skirtAlpha);
                ambientOcclusion *= 1.0h - caveMouthDistanceAo * 0.45h;
                albedo *= 1.0h - caveMouthDistanceAo * 0.24h;
                half bakedSedimentDust = saturate(bakedSedimentMask * smoothstep(0.55h, 0.92h, saturate(normalWS.y)) * (half)_GeologySedimentStrength);
                half horizontalDust = saturate(max(ResolveHorizontalSiltDust(normalWS), bakedSedimentDust));
                half4 siltSample = SampleCinematicAxisColor(TEXTURE2D_ARGS(_SiltLayerMap, sampler_SiltLayerMap), samplePositionWS, normalWS);
                half3 siltAlbedo = saturate(siltSample.rgb * (half3)_SiltTint.rgb * 1.2h);
                albedo = lerp(albedo, siltAlbedo, horizontalDust);
                smoothness = lerp(smoothness, min(smoothness, 0.22h), horizontalDust);
                half localCausticMask = ResolveLocalLightCaustic(samplePositionWS, normalWS, input.positionCS);
                if (xrFullQuality)
                {
                    HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);
                }
                HectonCoreLitApplyEnvironmentalWear(samplePositionWS, normalWS, (half)_EnvironmentalWear, (half3)_RustSaltColor.rgb, albedo, metallic, smoothness);
                half dissolveMalfunction = ApplyChunkDissolveMalfunction(input.positionCS, samplePositionWS, albedo, smoothness);
                half caveMouthPulse = ResolveCaveMouthPhosphorPulse(samplePositionWS, saturate(input.skirtAlpha));
                albedo = lerp(albedo, (half3)_ChunkDissolvePhosphorTint.rgb, caveMouthPulse * 0.08h);

                half3 litColor = EvaluateLighting(input.positionWS, input.positionCS, normalWS, SafeNormalize3(input.viewDirWS), albedo, metallic, smoothness, ambientOcclusion, localCausticMask);
                litColor += (half3)HectonCoreLitEvaluateGlowPointRadiance(samplePositionWS) * lerp(0.35h, 1.0h, decodedMask.emissionMask);
                litColor += (half3)_ChunkDissolvePhosphorTint.rgb * dissolveMalfunction * 0.16h;
                litColor += (half3)_ChunkDissolvePhosphorTint.rgb * caveMouthPulse * 0.10h;
                half thermalEmission = _CutScarEmission * recentHeatMask * lerp(0.22h, 1.0h, saturate(1.0h - recentHeatAge01));
                half3 emission = (_CutScarWarmColor.rgb * (_CutScarEmission * scarMask * 0.12h)) +
                    (thermalColor * thermalEmission) +
                    ((half3)_ChunkDissolvePhosphorTint.rgb * caveMouthPulse * 0.18h) +
                    ((half3)_ChunkDissolvePhosphorTint.rgb * decodedMask.emissionMask * 0.035h) +
                    ResolveOrganicBioluminescenceEmission(samplePositionWS, vertexCaveAo);
                half3 finalColor = MixFog(litColor + emission, input.fogFactor);
                finalColor = HectonCoreLitApplyXRFoveatedResolve(finalColor, input.xrFoveatedVector);
                return half4(finalColor, skirtCoverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                half globalCutMask = EvaluateGlobalCutMask(input.positionWS);
                half damageVolumeMask = globalCutMask >= 0.999h ? 0.0h : EvaluateDamageVolumeMask(input.positionWS);
                half cutMask = ResolveDearLieCarveMask(globalCutMask, damageVolumeMask);
                ApplyDearLieCarveClip(cutMask, input.positionCS.xy);
                half scarMask = FastVoxelPower01(cutMask, max(_CutScarSharpness, 0.5h));
                ResolveShadowCoverage(input.skirtAlpha, input.positionCS.xy, scarMask, input.positionWS);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 DepthFrag(ClipVaryings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                HectonCoreLitClipXRNearWallDither(input.xrNearClipFade, input.positionCS);
                LODFadeCrossFade(input.positionCS);
                ApplyChunkDissolveFade(input.positionCS);
                half globalCutMask = EvaluateGlobalCutMask(input.positionWS);
                half damageVolumeMask = globalCutMask >= 0.999h ? 0.0h : EvaluateDamageVolumeMask(input.positionWS);
                ApplyDearLieCarveClip(ResolveDearLieCarveMask(globalCutMask, damageVolumeMask), input.positionCS.xy);
                ResolveSkirtCoverage(input.skirtAlpha, input.positionCS.xy);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
