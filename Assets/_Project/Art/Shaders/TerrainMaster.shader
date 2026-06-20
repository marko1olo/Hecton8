Shader "HECTON/Terrain/TerrainMaster"
{
    Properties
    {
        [Header(Sand Layer)]
        _SandTex        ("Sand Albedo (RGB) Smooth (A)", 2D) = "white" {}
        _SandNormalStr  ("Sand Luma Detail Strength", Range(0,2)) = 0.35
        _SandScale      ("Sand Tiling", Float) = 0.1
        _SandColor      ("Sand Tint", Color) = (0.86, 0.78, 0.62, 1)

        [Header(Rock Layer)]
        _RockTex        ("Rock Albedo (RGB) Smooth (A)", 2D) = "gray" {}
        _RockScale      ("Rock Tiling", Float) = 0.1
        _RockColor      ("Rock Tint", Color) = (0.5, 0.5, 0.5, 1)
        _RockNormalStr  ("Rock Luma Detail Strength", Range(0,2)) = 1.0

        [Header(Silt Layer)]
        _SiltTex        ("Silt Albedo (RGB) Smooth (A)", 2D) = "gray" {}
        _SiltScale      ("Silt Tiling", Float) = 0.1
        _SiltColor      ("Silt Tint", Color) = (0.32, 0.36, 0.34, 1)
        _SiltNormalStr  ("Silt Luma Detail Strength", Range(0,2)) = 0.55

        [Header(Packed Control)]
        [NoScaleOffset] _TerrainControlRGBA ("Baked Biome Weight RGBA", 2D) = "black" {}
        _ControlScale ("Control UV Scale", Float) = 0.001953125

        [Header(Control Fallback)]
        _FallbackWaterlineY ("Fallback Waterline Y", Float) = 14.02
        _FallbackShoreWidth ("Fallback Shore Width", Float) = 18
        _FallbackHeightRange ("Fallback Height Range", Float) = 96

        [Header(Blending)]
        _StochasticStrength ("Stochastic Jitter", Range(0,1)) = 0.55

        [Header(Biome)]
        _BiomeTint      ("Biome Tint", Color) = (0.15, 0.12, 0.1, 1)
        [NoScaleOffset] _HectonBiomeGroundArray ("Biome Ground Texture Array", 2DArray) = "" {}
        _BiomeEdgeBleedScale ("Biome Edge Bleed Scale", Float) = 0.018
        _BiomeEdgeBleedStrength ("Biome Edge Bleed Strength", Range(0,1)) = 0.38
        _BiomeTransitionNoiseStrength ("Biome Transition Noise", Range(0,1)) = 0.42

        [Header(Sedimentation)]
        _SedimentStrength ("Sediment Strength", Range(0,1)) = 0.35
        _SedimentBlendWidth ("Sediment Blend Width", Range(0.001,0.5)) = 0.12

        [Header(Micro Erosion)]
        _FlowNormal ("Flow Normal Map", 2D) = "bump" {}
        _FlowNormalScale ("Flow Normal Scale", Float) = 0.035
        _FlowNormalStrength ("Flow Normal Strength", Range(0,2)) = 0.7
        _MicroErosionStrength ("Micro Erosion Strength", Range(0,1)) = 0.55
        _MicroBumpOffsetStrength ("Micro Bump Offset Strength", Range(0,0.08)) = 0.012
        _MicroBumpOffsetScale ("Micro Bump Offset Scale", Float) = 0.08

        [Header(Depth)]
        _DarkenPower    ("Depth Darken Power", Range(0.1,10)) = 2.5
        _DepthColor     ("Abyss Color", Color) = (0.02, 0.04, 0.08, 1)

        [Header(Cave Glow)]
        _CaveGlowColor  ("Cave Glow Color", Color) = (0.2, 0.8, 1.0, 1)
        _CaveGlowPower  ("Cave Glow Intensity", Range(0,10)) = 3.0

        [Header(Surface)]
        _Metallic       ("Metallic", Range(0,1)) = 0.0
        _BaseSmooth     ("Smoothness", Range(0,1)) = 0.5

        [Header(Planetary Canvas)]
        _FadeDistance   ("AUP Fade Distance", Float) = 2600
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ================================================================
        // SHARED HLSL - included in every pass automatically
        // ================================================================
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // -------------------------------------------------------------
        // SRP Batcher: ALL material properties in ONE contiguous CBUFFER.
        // No _ST floats needed - we use world-space UVs with _Scale.
        // -------------------------------------------------------------
        CBUFFER_START(UnityPerMaterial)
            float  _SandScale;
            float  _RockScale;
            float  _SiltScale;
            float  _RockNormalStr;
            float  _SandNormalStr;
            float  _SiltNormalStr;
            float  _ControlScale;
            float  _FallbackWaterlineY;
            float  _FallbackShoreWidth;
            float  _FallbackHeightRange;
            float  _StochasticStrength;
            float  _BiomeEdgeBleedScale;
            float  _BiomeEdgeBleedStrength;
            float  _BiomeTransitionNoiseStrength;
            float  _SedimentStrength;
            float  _SedimentBlendWidth;
            float  _FlowNormalScale;
            float  _FlowNormalStrength;
            float  _MicroErosionStrength;
            float  _MicroBumpOffsetStrength;
            float  _MicroBumpOffsetScale;
            float  _DarkenPower;
            float  _CaveGlowPower;
            float  _FadeDistance;
            half   _Metallic;
            half   _BaseSmooth;
            half4  _SandColor;
            half4  _RockColor;
            half4  _SiltColor;
            half4  _BiomeTint;
            half4  _DepthColor;
            half4  _CaveGlowColor;
        CBUFFER_END

        // Textures declared outside CBUFFER (URP requirement)
        TEXTURE2D(_SandTex);    SAMPLER(sampler_SandTex);
        TEXTURE2D(_RockTex);    SAMPLER(sampler_RockTex);
        TEXTURE2D(_SiltTex);    SAMPLER(sampler_SiltTex);
        TEXTURE2D(_TerrainControlRGBA); SAMPLER(sampler_TerrainControlRGBA);
        TEXTURE2D(_FlowNormal); SAMPLER(sampler_FlowNormal);
        TEXTURE2D(_HectonBiomeHeatmapTex); SAMPLER(sampler_HectonBiomeHeatmapTex);
        TEXTURE2D_ARRAY(_HectonBiomeGroundArray); SAMPLER(sampler_HectonBiomeGroundArray);
        TEXTURE2D(_HectonDistantTerrainShadowMask); SAMPLER(sampler_HectonDistantTerrainShadowMask);
        TEXTURE2D(_HectonVoxelBlendMask); SAMPLER(sampler_HectonVoxelBlendMask);
        TEXTURE3D(_HectonDamageVolumeTex); SAMPLER(sampler_HectonDamageVolumeTex);
        float4 _SargassumCanopyShadowParams;
        float4 _SargassumCanopyLightingParams;
        float4 _HectonBiomeHeatmapRect;
        float4 _HectonBiomeTextureParams;
        float4 _CurrentBiomeColor;
        float4 _HectonTerrainFadeParams;
        float4 _HectonTerrainFadeRuntimeOrigin;
        float4 _HectonTerrainFadeAupOrigin;
        float4 _HectonDistantTerrainShadowRect;
        float4 _HectonDistantTerrainShadowParams;
        float4 _HectonVoxelBlendMaskRect;
        float4 _HectonVoxelBlendMaskParams;
        float4 _HectonDamageVolumeWorldMin;
        float4 _HectonDamageVolumeInvSize;
        float _HectonDamageVolumeActive;
        float _HectonMathLodWeight;

        half3 HectonDominantAxisDirection(float3 value)
        {
            half3 v = (half3)value;
            half dominant = max(max(abs(v.x), abs(v.y)), abs(v.z));
            return v * rcp(max(dominant, 0.0001h));
        }

        half HectonTerrainMathLodWeight()
        {
            return (half)saturate(isfinite(_HectonMathLodWeight) ? _HectonMathLodWeight : 1.0);
        }

        half EvaluateSargassumCanopyShadow(float3 positionWS)
        {
            half enabled = step(0.5h, (half)_SargassumCanopyLightingParams.w);
            float2 delta = positionWS.xz - _SargassumCanopyShadowParams.xy;
            float distanceSq01 = dot(delta, delta) * (_SargassumCanopyShadowParams.z * _SargassumCanopyShadowParams.z);
            half radialFalloff = saturate(1.0 - distanceSq01);
            radialFalloff *= radialFalloff;
            half canopyWindow = saturate(_SargassumCanopyLightingParams.z);
            half canopyOcclusion = saturate(_SargassumCanopyShadowParams.w) * (1.0h - canopyWindow * 0.55h);
            return radialFalloff * canopyOcclusion * enabled;
        }

        half EvaluatePlanetaryTerrainFade(float3 positionWS)
        {
            half enabled = step(0.5h, (half)_HectonTerrainFadeParams.z);
            float fadeDistance = max(max(_FadeDistance, _HectonTerrainFadeParams.x), 1.0);
            float fadeWidth = max(rcp(max(_HectonTerrainFadeParams.y, 0.0001)), 1.0);
            float2 fadeDelta = positionWS.xz - _HectonTerrainFadeRuntimeOrigin.xz;
            float distanceSqXZ = dot(fadeDelta, fadeDelta);
            float fadeStart = max(0.0, fadeDistance - fadeWidth);
            float fadeStartSq = fadeStart * fadeStart;
            float fadeEndSq = fadeDistance * fadeDistance;
            float fadeInvRangeSq = rcp(max(fadeEndSq - fadeStartSq, 1.0));
            half fade = (half)(1.0 - saturate((distanceSqXZ - fadeStartSq) * fadeInvRangeSq));
            return lerp(1.0h, fade, enabled);
        }

        half HectonInterleavedGradientNoise(float2 pixel)
        {
            return (half)frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        half ApplyPlanetaryDitherFade(half fade, float4 positionCS)
        {
            half edgeBand = saturate(1.0h - abs(fade * 2.0h - 1.0h));
            half noise = HectonInterleavedGradientNoise(positionCS.xy);
            half strength = saturate((half)_HectonTerrainFadeParams.w);
            return saturate(fade + (noise - 0.5h) * edgeBand * strength);
        }

        half HectonDitheredSaturateRamp(half value, half edge0, half invWidth, half ditherWidth, half ign01)
        {
            half ditheredEdge = edge0 + (ign01 - 0.5h) * ditherWidth;
            return saturate((value - ditheredEdge) * invWidth);
        }

        half EvaluateDistantTerrainHeightShadow(float3 positionWS)
        {
            float2 uv = (positionWS.xz - _HectonDistantTerrainShadowRect.xy) * _HectonDistantTerrainShadowRect.zw;
            half enabled = step(0.5h, (half)_HectonDistantTerrainShadowParams.z);
            half inside = step(0.0h, (half)uv.x) * step(0.0h, (half)uv.y) *
                step((half)uv.x, 1.0h) * step((half)uv.y, 1.0h);
            [branch]
            if (enabled * inside <= 0.0h)
                return 0.0h;

            half mask = SAMPLE_TEXTURE2D(_HectonDistantTerrainShadowMask, sampler_HectonDistantTerrainShadowMask, uv).r;
            return saturate(mask * (half)_HectonDistantTerrainShadowParams.x);
        }

        half EvaluateHectonVoxelBlendMask(float3 positionWS)
        {
            float2 uv = (positionWS.xz - _HectonVoxelBlendMaskRect.xy) * _HectonVoxelBlendMaskRect.zw;
            half enabled = step(0.5h, (half)_HectonVoxelBlendMaskParams.x);
            half inside = step(0.0h, (half)uv.x) * step(0.0h, (half)uv.y) *
                step((half)uv.x, 1.0h) * step((half)uv.y, 1.0h);
            float2 maskUv = saturate(uv * max(_HectonVoxelBlendMaskParams.zw, float2(0.0, 0.0)));
            half mask = SAMPLE_TEXTURE2D(_HectonVoxelBlendMask, sampler_HectonVoxelBlendMask, maskUv).r;
            return saturate(mask * (half)_HectonVoxelBlendMaskParams.y * enabled * inside);
        }

        half EvaluateHectonDearLieDamageVolume(float3 positionWS)
        {
            if (_HectonDamageVolumeActive < 0.5)
                return 0.0h;

            float3 uvw = (positionWS - _HectonDamageVolumeWorldMin.xyz) * _HectonDamageVolumeInvSize.xyz;
            if (uvw.x < 0.0 || uvw.x > 1.0 || uvw.y < 0.0 || uvw.y > 1.0 || uvw.z < 0.0 || uvw.z > 1.0)
                return 0.0h;

            return SAMPLE_TEXTURE3D_LOD(_HectonDamageVolumeTex, sampler_HectonDamageVolumeTex, uvw, 0).r;
        }

        void ApplyHectonDearLieTerrainClip(float3 positionWS, float2 positionCS)
        {
            half damageMask = saturate(EvaluateHectonDearLieDamageVolume(positionWS));
            half clipStrength = saturate((damageMask - 0.45h) * 1.8181818h);
            half coverage = saturate(1.0h - clipStrength);
            clip(coverage - HectonInterleavedGradientNoise(positionCS) * 0.125h);
        }

        half HectonCellNoise2D(float2 position)
        {
            return HectonInterleavedGradientNoise(floor(position));
        }

        void HectonResolveMissingControlWeights(
            float3 positionWS,
            float3 normalWS,
            half ign01,
            out half rockWeight,
            out half sandWeight,
            out half siltWeight,
            out half sedimentMask)
        {
            half upAxis = saturate(abs(HectonDominantAxisDirection(normalWS).y));
            half steepRock = saturate((0.74h - upAxis) * 3.8461538h);
            float shoreWidth = max(_FallbackShoreWidth, 0.001);
            float heightRange = max(_FallbackHeightRange, 1.0);
            float waterDelta = positionWS.y - _FallbackWaterlineY;
            half waterlineBand = saturate((half)(1.0 - abs(waterDelta) / shoreWidth));
            half belowWater = saturate((half)((_FallbackWaterlineY - positionWS.y) / heightRange));
            half terraceNoise = (ign01 - 0.5h) * 0.16h;

            rockWeight = saturate(steepRock + waterlineBand * 0.18h + terraceNoise);
            siltWeight = saturate(belowWater * 0.62h + waterlineBand * 0.24h - steepRock * 0.35h);
            sandWeight = saturate((1.0h - rockWeight) * (1.0h - siltWeight * 0.45h));

            half total = max(rockWeight + sandWeight + siltWeight, 0.001h);
            rockWeight *= rcp(total);
            sandWeight *= rcp(total);
            siltWeight *= rcp(total);
            sedimentMask = saturate(siltWeight * 0.65h + waterlineBand * 0.22h);
        }

        half HectonCheapSharp01(half value, float sharpness)
        {
            half curved = saturate(abs(value));
            half curved2 = curved * curved;
            half weight = saturate((half)((sharpness - 1.0) * 0.0322580645));
            return lerp(curved, curved2, weight);
        }

        float2 HectonResolveHexJitter(float2 position, half strength)
        {
            float jitter = saturate(strength);
            float2 hexCoord = float2(position.x + position.y * 0.57735027, position.y * 1.15470054);
            float2 cell = floor(hexCoord);
            cell.x += frac(cell.y * 0.5);
            float hash = HectonInterleavedGradientNoise(cell);
            return (float2(hash, frac(hash * 17.17)) - 0.5) * jitter;
        }

        half4 HectonSampleStochastic2D(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 jitterOffset)
        {
            return SAMPLE_TEXTURE2D(tex, samp, uv + jitterOffset);
        }

        half3 HectonUnpackNormalRG(half4 packedNormal, half strength)
        {
            half3 normalTS;
            normalTS.xy = packedNormal.xy * 2.0h - 1.0h;
            normalTS.xy *= strength;
            normalTS.z = 1.0h;
            return normalTS;
        }

        half3 HectonResolveXZNormalOffset(half3 normalTS)
        {
            return half3(normalTS.x, normalTS.z - 1.0h, normalTS.y);
        }

        float2 HectonResolveMicroBumpOffset(float3 positionWS, half3 viewDirectionWS, half rockWeight, half steepMask)
        {
            float scale = max(_MicroBumpOffsetScale, 0.0001);
            half height = HectonCellNoise2D(positionWS.xz * scale);
            half centeredHeight = height - 0.5h;
            half strength = saturate((half)_MicroBumpOffsetStrength) * saturate(rockWeight + steepMask);
            return (float2)viewDirectionWS.xz * (centeredHeight * strength);
        }

        half ResolveBiomeEdgeBleed(float3 positionWS, half biome)
        {
            half transitionMask = saturate(1.0h - abs(biome * 2.0h - 1.0h));
            float scale = max(_BiomeEdgeBleedScale, 0.0001);
            half edgeNoise = HectonCellNoise2D(positionWS.xz * scale);
            half bleed = (edgeNoise - 0.5h) * transitionMask * (half)_BiomeEdgeBleedStrength;
            half gradientDither = (edgeNoise - 0.5h) * transitionMask * (half)_BiomeTransitionNoiseStrength;
            return saturate(biome + bleed + gradientDither);
        }

        half HectonLoadBiomeHeatmapId(int2 pixel)
        {
            int maxPixel = (int)max(_HectonBiomeTextureParams.z, 1.0);
            int2 safePixel = clamp(pixel, int2(0, 0), int2(maxPixel, maxPixel));
            return _HectonBiomeHeatmapTex.Load(int3(safePixel, 0)).r;
        }

        half HectonSelectBiomeIdByIgn(half b00, half b10, half b01, half b11, float2 fracPart, half ign01)
        {
            half2 f = saturate((half2)fracPart);
            half w00 = (1.0h - f.x) * (1.0h - f.y);
            half w10 = f.x * (1.0h - f.y);
            half w01 = (1.0h - f.x) * f.y;
            half c00 = w00;
            half c10 = c00 + w10;
            half c01 = c10 + w01;
            return ign01 < c00 ? b00 : (ign01 < c10 ? b10 : (ign01 < c01 ? b01 : b11));
        }

        half4 HectonSampleDitheredBiomeArray(float3 positionWS, float4 positionCS, float2 jitterOffset, out uint selectedSlice)
        {
            float2 heatUv = saturate((positionWS.xz - _HectonBiomeHeatmapRect.xy) * _HectonBiomeHeatmapRect.zw);
            float heatmapMaxPixel = max(_HectonBiomeTextureParams.z, 1.0);
            float2 heatCoord = heatUv * heatmapMaxPixel;
            int2 pixel00 = (int2)floor(heatCoord);
            int2 pixel11 = min(pixel00 + int2(1, 1), int2((int)heatmapMaxPixel, (int)heatmapMaxPixel));
            half b00 = HectonLoadBiomeHeatmapId(pixel00);
            half b10 = HectonLoadBiomeHeatmapId(int2(pixel11.x, pixel00.y));
            half b01 = HectonLoadBiomeHeatmapId(int2(pixel00.x, pixel11.y));
            half b11 = HectonLoadBiomeHeatmapId(pixel11);
            half chosen = HectonSelectBiomeIdByIgn(b00, b10, b01, b11, frac(heatCoord), HectonInterleavedGradientNoise(positionCS.xy));
            uint maxSlice = (uint)max(_HectonBiomeTextureParams.y - 1.0, 0.0);
            uint encodedBiomeId = (uint)(chosen * 255.0h + 0.5h);
            selectedSlice = encodedBiomeId > 0u ? min(encodedBiomeId - 1u, maxSlice) : 0u;
            float2 biomeUv = positionWS.xz * max(_HectonBiomeTextureParams.x, 0.0001) + jitterOffset;
            return SAMPLE_TEXTURE2D_ARRAY(_HectonBiomeGroundArray, sampler_HectonBiomeGroundArray, biomeUv, selectedSlice);
        }

        ENDHLSL

        // ================================================================
        // PASS 1: UniversalForward
        // ================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ForwardVert
            #pragma fragment ForwardFrag

            // Shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // Lightmaps
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            // Light layers, reflection probes
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
            // Fog
            #pragma multi_compile_fog
            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

            // -- Vertex color packing: R=reserved G=Depth B=Cave A=Biome --

            struct ForwardAttributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                float2 staticLightmapUV  : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ForwardVaryings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                // Pack vertex colors: xy = (reserved, depth), zw = (cave, biome)
                half4  vColor      : TEXCOORD2;
                half   fogFactor   : TEXCOORD3;
                // Keep interpolants explicit for the SRP batcher.
                #if defined(LIGHTMAP_ON)
                float2 staticLightmapUV  : TEXCOORD5;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                float2 dynamicLightmapUV : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ForwardVaryings ForwardVert(ForwardAttributes IN)
            {
                ForwardVaryings OUT = (ForwardVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.vColor     = IN.color;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                #if defined(LIGHTMAP_ON)
                OUT.staticLightmapUV = IN.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                OUT.dynamicLightmapUV = IN.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                return OUT;
            }

            half4 ForwardFrag(ForwardVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                ApplyHectonDearLieTerrainClip(IN.positionWS, IN.positionCS.xy);

                // ---- Unpack Vertex Colors ----
                half depth = IN.vColor.g;
                half cave  = IN.vColor.b;
                half biome = IN.vColor.a;
                // ---- Baked biome-mask terrain sampling ----
                half3 viewDirectionWS = HectonDominantAxisDirection(_WorldSpaceCameraPos.xyz - IN.positionWS);
                float2 sandUv = IN.positionWS.xz * max(_SandScale, 0.0001);
                float2 rockUv = IN.positionWS.xz * max(_RockScale, 0.0001);
                float2 siltUv = IN.positionWS.xz * max(_SiltScale, 0.0001);
                half screenIgn = HectonInterleavedGradientNoise(IN.positionCS.xy);
                half4 control = SAMPLE_TEXTURE2D(_TerrainControlRGBA, sampler_TerrainControlRGBA, IN.positionWS.xz * max(_ControlScale, 0.000001));
                // SHINOBU_243: R=Rock, G=Sand, B=ambient silt, A=erosion-deposited silt.
                half controlSum = control.r + control.g + control.b + control.a;
                half hasControl = step(0.001h, controlSum);
                half controlInvSum = rcp(max(controlSum, 0.001h));
                half controlRock = control.r * controlInvSum;
                half controlSand = control.g * controlInvSum;
                half controlSilt = saturate((control.b + control.a) * controlInvSum);
                half rockWeight = saturate(controlRock);
                half sandWeight = saturate(controlSand);
                half siltWeight = saturate(controlSilt);
                half sedimentSource = control.a;
                [branch]
                if (hasControl < 0.5h)
                {
                    HectonResolveMissingControlWeights(IN.positionWS, IN.normalWS, screenIgn, rockWeight, sandWeight, siltWeight, sedimentSource);
                }
                half bakedWeightTotal = max(rockWeight + sandWeight + siltWeight, 0.001h);
                rockWeight *= rcp(bakedWeightTotal);
                sandWeight *= rcp(bakedWeightTotal);
                siltWeight *= rcp(bakedWeightTotal);
                half stochasticStrength = saturate((half)_StochasticStrength);
                float jitterGridScale = max(max(max(_SandScale, _RockScale), _SiltScale), 0.0001);
                float2 stochasticJitter = HectonResolveHexJitter(IN.positionWS.xz * jitterGridScale, stochasticStrength);
                half steepMask = saturate(max(rockWeight, sedimentSource)) * (half)_MicroErosionStrength;
                float2 microBumpOffset = HectonResolveMicroBumpOffset(IN.positionWS, viewDirectionWS, rockWeight, steepMask);
                sandUv += microBumpOffset * 0.35;
                rockUv += microBumpOffset;
                siltUv += microBumpOffset * 0.18;
                half useBiomeArray = step(0.5h, (half)_HectonBiomeTextureParams.w);
                uint selectedBiomeSlice = 0u;
                half4 biomeArraySample = half4(1.0h, 1.0h, 1.0h, 0.5h);
                half4 sandSample = half4(1.0h, 1.0h, 1.0h, 0.5h);
                half4 rockSample = half4(1.0h, 1.0h, 1.0h, 0.5h);
                half4 siltSample = half4(1.0h, 1.0h, 1.0h, 0.5h);
                [branch]
                if (useBiomeArray > 0.0h)
                {
                    biomeArraySample = HectonSampleDitheredBiomeArray(IN.positionWS, IN.positionCS, stochasticJitter, selectedBiomeSlice);
                    
                    // Biome array acts as the diverse Ground (Sand) for the region.
                    // Rock (cliffs) and Silt (sediment) retain global physical properties.
                    sandSample = biomeArraySample;
                    rockSample = HectonSampleStochastic2D(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), rockUv, stochasticJitter);
                    siltSample = HectonSampleStochastic2D(TEXTURE2D_ARGS(_SiltTex, sampler_SiltTex), siltUv, stochasticJitter * 0.5);
                }
                else
                {
                    sandSample = HectonSampleStochastic2D(TEXTURE2D_ARGS(_SandTex, sampler_SandTex), sandUv, stochasticJitter);
                    rockSample = HectonSampleStochastic2D(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), rockUv, stochasticJitter);
                    siltSample = HectonSampleStochastic2D(TEXTURE2D_ARGS(_SiltTex, sampler_SiltTex), siltUv, stochasticJitter * 0.5);
                }

                // Crisp height-based blending (safe against 0-weights and 0-heights)
                half rockH = rockWeight * (rockSample.a + 0.05h);
                half sandH = sandWeight * (sandSample.a + 0.05h);
                half siltH = siltWeight * (siltSample.a + 0.05h);
                
                half rockBlend = rockH * rockH * rockH;
                half sandBlend = sandH * sandH * sandH;
                half siltBlend = siltH * siltH * siltH;
                
                half heightBlendTotal = max(rockBlend + sandBlend + siltBlend, 0.0001h);
                rockWeight = rockBlend * rcp(heightBlendTotal);
                sandWeight = sandBlend * rcp(heightBlendTotal);
                siltWeight = siltBlend * rcp(heightBlendTotal);

                // Apply voxel seam override AFTER height blending to guarantee cave seams are rocky
                half voxelBlendMask = EvaluateHectonVoxelBlendMask(IN.positionWS);
                rockWeight = saturate(max(rockWeight, voxelBlendMask));
                sandWeight = sandWeight * saturate(1.0h - voxelBlendMask);
                siltWeight = siltWeight * saturate(1.0h - voxelBlendMask);
                
                half postVoxelTotal = max(rockWeight + sandWeight + siltWeight, 0.0001h);
                rockWeight *= rcp(postVoxelTotal);
                sandWeight *= rcp(postVoxelTotal);
                siltWeight *= rcp(postVoxelTotal);

                half taaMicroBump = (screenIgn - 0.5h) * steepMask * 0.035h;
                half terrainMathLodWeight = HectonTerrainMathLodWeight();
                half2 cheapMaterialRgOffset = half2(taaMicroBump, -taaMicroBump);
                half2 materialRgOffset = cheapMaterialRgOffset;
                if (terrainMathLodWeight > 0.0001h)
                {
                    half sandLuma = dot(sandSample.rgb, half3(0.25h, 0.5h, 0.25h));
                    half rockLuma = dot(rockSample.rgb, half3(0.25h, 0.5h, 0.25h));
                    half siltLuma = dot(siltSample.rgb, half3(0.25h, 0.5h, 0.25h));
                    half weightedLuma = (sandLuma * sandWeight) + (rockLuma * rockWeight) + (siltLuma * siltWeight);
                    half materialLuma = weightedLuma; 
                    half weightedDetail = (half)_SandNormalStr * sandWeight + (half)_RockNormalStr * rockWeight + (half)_SiltNormalStr * siltWeight;
                    half materialDetailStrength = weightedDetail * 0.16h;
                    half2 detailMaterialRgOffset = half2(ddx(materialLuma), ddy(materialLuma)) * materialDetailStrength;
                    detailMaterialRgOffset += cheapMaterialRgOffset;
                    materialRgOffset = lerp(cheapMaterialRgOffset, detailMaterialRgOffset, terrainMathLodWeight);
                }
                half3 blendedNormalOffset = half3(materialRgOffset.x, 0.0h, materialRgOffset.y);
                half3 sandAlbedo = useBiomeArray > 0.0h ? sandSample.rgb : sandSample.rgb * _SandColor.rgb;
                half3 rockAlbedo = rockSample.rgb * _RockColor.rgb;
                half3 siltAlbedo = siltSample.rgb * _SiltColor.rgb;
                half3 albedo = (sandAlbedo * sandWeight) + (rockAlbedo * rockWeight) + (siltAlbedo * siltWeight);
                half weightedSmooth = (sandSample.a * sandWeight) + (rockSample.a * rockWeight) + (siltSample.a * siltWeight);
                half smoothness = weightedSmooth * _BaseSmooth;

                // ---- Biome tint ----
                half biomeBleed = ResolveBiomeEdgeBleed(IN.positionWS, biome);
                half3 biomeTint = lerp(_BiomeTint.rgb, (half3)_CurrentBiomeColor.rgb, useBiomeArray * saturate((half)_CurrentBiomeColor.a));
                albedo = lerp(albedo, albedo * biomeTint, biomeBleed);

                // ---- Depth darkening ----
                half depthFactor = HectonCheapSharp01(depth, _DarkenPower);
                albedo     = lerp(albedo, albedo * _DepthColor.rgb, depthFactor);
                smoothness = smoothness * (1.0 - depthFactor);

                // ---- Cave emission ----
                half3 emission = cave * _CaveGlowColor.rgb * _CaveGlowPower;
                half canopyShadow = EvaluateSargassumCanopyShadow(IN.positionWS);
                albedo = lerp(albedo, albedo * 0.34h, canopyShadow);
                emission *= 1.0h - canopyShadow * 0.8h;
                half distantHeightShadow = EvaluateDistantTerrainHeightShadow(IN.positionWS);
                albedo = lerp(albedo, albedo * 0.20h, distantHeightShadow);
                emission *= 1.0h - distantHeightShadow * 0.9h;
                // ---- Final world normal ----
                half flowHash = screenIgn - 0.5h;
                half3 cheapFlowNormalWS = half3(flowHash, 0.0h, -flowHash) * (half)_FlowNormalStrength;
                half3 flowNormalWS = cheapFlowNormalWS;
                if (terrainMathLodWeight > 0.0001h)
                {
                    float flowScale = max(_FlowNormalScale, 0.0001);
                    float2 flowUv = IN.positionWS.xz * flowScale +
                        float2(IN.positionWS.y * flowScale * 2.13, IN.positionWS.y * flowScale * 0.37);
                    half3 sampledFlowNormalWS = HectonResolveXZNormalOffset(
                        HectonUnpackNormalRG(
                            SAMPLE_TEXTURE2D(_FlowNormal, sampler_FlowNormal, flowUv),
                            (half)_FlowNormalStrength));
                    flowNormalWS = lerp(cheapFlowNormalWS, sampledFlowNormalWS, terrainMathLodWeight);
                }
                half3 finalNormalWS = HectonDominantAxisDirection(
                    IN.normalWS + blendedNormalOffset + flowNormalWS * steepMask);
                half sedimentMask = saturate(sedimentSource) * (half)_SedimentStrength;
                albedo = lerp(albedo, siltAlbedo, sedimentMask);
                smoothness = lerp(smoothness, smoothness * 0.55h, sedimentMask);

                // ============================================================
                // InputData - fully initialized to prevent magenta
                // ============================================================
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.positionCS              = IN.positionCS;
                inputData.normalWS                = finalNormalWS;
                inputData.viewDirectionWS         = viewDirectionWS;
                inputData.fogCoord                = InitializeInputDataFog(
                                                        float4(IN.positionWS, 1.0), IN.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask              = half4(1.0, 1.0, 1.0, 1.0);

                #if defined(LIGHTMAP_ON)
                    inputData.staticLightmapUV    = IN.staticLightmapUV;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.dynamicLightmapUV   = IN.dynamicLightmapUV;
                #endif

                // Shadow coord
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = ComputeScreenPos(IN.positionCS);
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif

                // ---- Ambient / GI ----
                #if defined(LIGHTMAP_ON)
                    // Use lightmap for baked GI
                    #if defined(DYNAMICLIGHTMAP_ON)
                        inputData.bakedGI = SAMPLE_GI(
                            IN.staticLightmapUV,
                            IN.dynamicLightmapUV,
                            half3(0, 0, 0),
                            finalNormalWS);
                    #else
                        inputData.bakedGI = SAMPLE_GI(
                            IN.staticLightmapUV,
                            half3(0, 0, 0),
                            finalNormalWS);
                    #endif
                #else
                    inputData.bakedGI = H8CustomLightProbeResolveAmbient(IN.positionWS, finalNormalWS, half3(0.015h, 0.025h, 0.035h));
                #endif

                // ============================================================
                // SurfaceData - fully initialized to prevent magenta
                // ============================================================
                SurfaceData surfData    = (SurfaceData)0;
                surfData.albedo         = albedo;
                surfData.metallic       = _Metallic;
                surfData.specular       = half3(0.0, 0.0, 0.0);
                surfData.smoothness     = smoothness;
                surfData.normalTS       = half3(0.0, 0.0, 1.0);
                surfData.emission       = emission;
                surfData.occlusion      = 1.0;
                surfData.alpha          = 1.0;
                surfData.clearCoatMask  = 0.0;
                surfData.clearCoatSmoothness = 0.0;

                // ---- PBR lighting ----
                half4 color = UniversalFragmentPBR(inputData, surfData);

                // ---- Fog ----
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                half planetaryFade = ApplyPlanetaryDitherFade(EvaluatePlanetaryTerrainFade(IN.positionWS), IN.positionCS);
                half3 noirFogFloor = half3(0.0015h, 0.0023h, 0.0031h);
                color.rgb = lerp(noirFogFloor, color.rgb, planetaryFade);

                return color;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 2: ShadowCaster
        // ================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // These are set by URP shadow system
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVary
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVary ShadowVert(ShadowAttr IN)
            {
                ShadowVary OUT = (ShadowVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = HectonDominantAxisDirection(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, nrmWS, lightDir));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = posCS;
                OUT.positionWS = posWS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVary IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                ApplyHectonDearLieTerrainClip(IN.positionWS, IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 3: DepthOnly
        // ================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest  LEqual
            Cull   Back
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            struct DepthAttr
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVary
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVary DepthVert(DepthAttr IN)
            {
                DepthVary OUT = (DepthVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 DepthFrag(DepthVary IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                ApplyHectonDearLieTerrainClip(IN.positionWS, IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        // ================================================================
        // PASS 4: DepthNormals - manual pass, avoids Unity 6 include bugs
        // Uses the same cheap sampling path as the forward pass.
        // ================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            // Only Core.hlsl is included via HLSLINCLUDE - minimal dependencies

            struct DNAttr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVary
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVary DNVert(DNAttr IN)
            {
                DNVary OUT = (DNVary)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            half4 DNFrag(DNVary IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                ApplyHectonDearLieTerrainClip(IN.positionWS, IN.positionCS.xy);

                float3 finalN = HectonDominantAxisDirection(IN.normalWS);

                // Encode for URP DepthNormals texture
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(finalN);           // values between [-1, +1]
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5); // values between [ 0,  1]
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);    // values between [ 0,  1]
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(finalN, 0.0);
                #endif
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
