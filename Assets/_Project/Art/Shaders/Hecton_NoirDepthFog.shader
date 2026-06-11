Shader "Hidden/Hecton8/NoirDepthFog"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
        #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Hecton_WaterExtinction.hlsl"

        CBUFFER_START(HectonNoirDepthFogGlobals)
            float4 _HectonNoirDepthFogShallowColor;
            float4 _HectonNoirDepthFogAbyssColor;
            float4 _HectonNoirDepthFogParamsA; // x=visual density, y=start meters, z=max meters, w=reserved
            float4 _HectonNoirDepthFogParamsB; // x=quality, y=surface fog weight, z=reserved, w=dither strength
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        Texture2D<int> _HectonMarineSnowFogDensityTex;
        float4 _HectonMarineSnowFogDensityTexelSize;
        float4 _HectonMarineSnowFogDensityParams;

        struct Attributes
        {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float2 ResolveFoveatedSourceUV(float2 uv)
        {
            return FoveatedRemapLinearToNonUniform(uv);
        }

        float ResolveRawDepthValidity(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float HectonNoirDepthFogFinite(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float HectonNoirDepthFogSafePositiveRcp(float value, float fallbackValue, float minimumValue)
        {
            float safeValue = max(HectonNoirDepthFogFinite(value, fallbackValue), minimumValue);
            return rcp(safeValue);
        }

        float ResolveTaaDitherPhaseNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            uint2 pixelParity = (uint2)pixel & 1u;
            uint phaseIndex = pixelParity.x | (pixelParity.y << 1u);
            float2 taaPhase = float2((float)(phaseIndex & 1u), (float)((phaseIndex >> 1u) & 1u)) * 0.5;
            return frac(52.9829189 * frac(dot(pixel + taaPhase, float2(0.06711056, 0.00583715))));
        }

        float SampleMarineSnowFogDensity(float2 screenUV)
        {
            [branch]
            if (_HectonMarineSnowFogDensityParams.w <= 0.5 ||
                _HectonMarineSnowFogDensityParams.x <= 0.0001 ||
                _HectonMarineSnowFogDensityTexelSize.z < 1.0 ||
                _HectonMarineSnowFogDensityTexelSize.w < 1.0)
            {
                return 0.0;
            }

            int2 pixel = int2(
                saturate(screenUV.x) * (_HectonMarineSnowFogDensityTexelSize.z - 1.0) + 0.5,
                saturate(screenUV.y) * (_HectonMarineSnowFogDensityTexelSize.w - 1.0) + 0.5);
            int rawDensity = _HectonMarineSnowFogDensityTex.Load(int3(pixel, 0)).r;
            float decodedDensity = saturate(rawDensity * HectonNoirDepthFogSafePositiveRcp(_HectonMarineSnowFogDensityParams.y, 1.0, 1.0));
            return saturate(decodedDensity * max(HectonNoirDepthFogFinite(_HectonMarineSnowFogDensityParams.x, 0.0), 0.0));
        }

        float FastNegativeExp(float value)
        {
            value = max(HectonNoirDepthFogFinite(value, 0.0), 0.0);
            float valueSq = value * value;
            return HectonNoirDepthFogSafePositiveRcp(1.0 + value + 0.48 * valueSq + 0.235 * valueSq * value, 1.0, 0.000001);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 cameraTextureUv = ResolveFoveatedSourceUV(input.screenUV);
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, cameraTextureUv);
            float rawDepth = SampleSceneDepth(cameraTextureUv);
            float depthValid = ResolveRawDepthValidity(rawDepth);
            [branch]
            if (depthValid <= 0.5)
                return sourceColor;

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            float fogStartMeters = max(HectonNoirDepthFogFinite(_HectonNoirDepthFogParamsA.y, 0.0), 0.0);
            float invDepthRange = HectonNoirDepthFogSafePositiveRcp(_HectonNoirDepthFogParamsA.z - fogStartMeters, 1.0, 1.0);
            half depth01 = (half)saturate((linearEyeDepth - fogStartMeters) * invDepthRange);
            [branch]
            if (depth01 <= 0.0001h)
                return sourceColor;

            float quality01 = saturate(HectonNoirDepthFogFinite(_HectonNoirDepthFogParamsB.x, 1.0));
            float qualityCurve = quality01 * quality01 * (3.0 - 2.0 * quality01);
            half surfaceFogWeight = (half)saturate(HectonNoirDepthFogFinite(_HectonNoirDepthFogParamsB.y, 1.0));
            [branch]
            if (surfaceFogWeight <= 0.0001h)
                return sourceColor;

            float fogDepthMeters = max(0.0, linearEyeDepth - fogStartMeters);
            float visualDensity = max(HectonNoirDepthFogFinite(_HectonNoirDepthFogParamsA.x, 0.0), 0.000001);
            half fogRaw = (half)(1.0 - FastNegativeExp(fogDepthMeters * visualDensity));
            half fogFactor = fogRaw * fogRaw * (0.82h + fogRaw * 0.18h);
            half depthSq = depth01 * depth01;
            fogFactor = saturate(fogFactor * lerp(0.62h, 1.08h, depth01));
            fogFactor = saturate(fogFactor + (half)SampleMarineSnowFogDensity(input.screenUV));
            fogFactor = saturate(fogFactor * (half)lerp(0.92, 1.08, qualityCurve) * surfaceFogWeight);

            half transitionEdge = saturate(1.0h - abs(fogFactor - 0.5h) * 2.0h);
            half ditherStrength = saturate((half)_HectonNoirDepthFogParamsB.w) * (half)lerp(0.35, 1.0, qualityCurve) * surfaceFogWeight;
            half dither = (half)(ResolveTaaDitherPhaseNoise(input.screenUV) - 0.5) * ditherStrength * lerp(0.0039215686h, 0.015625h, transitionEdge);
            fogFactor = saturate(fogFactor + dither);

            half3 fogColor = (half3)lerp(
                _HectonNoirDepthFogShallowColor.rgb,
                _HectonNoirDepthFogAbyssColor.rgb,
                saturate(depthSq + depth01 * 0.18h));
            half3 extinctionColor = H8WaterExtinctionResolveRgbByDepthMeters(linearEyeDepth, (half)_ExtinctionLUTRuntime.y);
            half extinctionBlend = H8WaterExtinctionFogBlend();
            half3 abyssColor = max((half3)_HectonNoirDepthFogAbyssColor.rgb, half3(0.0h, 0.0h, 0.0h));
            half3 extinctionFloor = max((half3)_HectonNoirDepthFogShallowColor.rgb, abyssColor);
            fogColor = H8WaterExtinctionApplyFogTint(fogColor, extinctionColor, extinctionBlend, extinctionFloor, abyssColor);
            sourceColor.rgb = lerp(sourceColor.rgb, sourceColor.rgb * extinctionColor, fogFactor * extinctionBlend * 0.35h);
            sourceColor.rgb = lerp(sourceColor.rgb, fogColor, (half)fogFactor);
            return sourceColor;
        }
        ENDHLSL

        Pass
        {
            Name "NoirDepthFog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}
