Shader "Hidden/Hecton8/NoirDepthFog"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5
        #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
        #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _HectonNoirDepthFogShallowColor;
            float4 _HectonNoirDepthFogAbyssColor;
            float4 _HectonNoirDepthFogParamsA; // x=visual density, y=start meters, z=max meters, w=reserved
            float4 _HectonNoirDepthFogParamsB; // x/y/z=reserved, w=dither strength
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        Texture2D<int> _HectonMarineSnowFogDensityTex;
        float4 _HectonMarineSnowFogDensityTexelSize;
        float4 _HectonMarineSnowFogDensityParams;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float ResolveRawDepthValidity(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float ResolveTaaDitherPhaseNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _ScaledScreenParams.xy);
            uint phaseIndex = _TaaFrameIndex & 3u;
            float2 taaPhase = float2((float)(phaseIndex & 1u), (float)((phaseIndex >> 1u) & 1u)) * 0.5;
            return frac(52.9829189 * frac(dot(pixel + taaPhase, float2(0.06711056, 0.00583715))));
        }

        float SampleMarineSnowFogDensity(float2 screenUV)
        {
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
            float decodedDensity = saturate(rawDensity * rcp(max(_HectonMarineSnowFogDensityParams.y, 1.0)));
            return saturate(decodedDensity * _HectonMarineSnowFogDensityParams.x);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            float rawDepth = SampleSceneDepth(input.screenUV);
            float depthValid = ResolveRawDepthValidity(rawDepth);
            if (depthValid <= 0.5)
                return sourceColor;

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            float fogStartMeters = max(_HectonNoirDepthFogParamsA.y, 0.0);
            float invDepthRange = rcp(max(_HectonNoirDepthFogParamsA.z - fogStartMeters, 1.0));
            half depth01 = (half)saturate((linearEyeDepth - fogStartMeters) * invDepthRange);
            if (depth01 <= 0.0001h)
                return sourceColor;

            half depthSq = depth01 * depth01;
            half filmRamp = depthSq * (3.0h - 2.0h * depth01);
            half densityGain = saturate((half)_HectonNoirDepthFogParamsA.x * 96.0h);
            half fogFactor = saturate(filmRamp * lerp(0.42h, 1.16h, densityGain));
            fogFactor = saturate(fogFactor + (half)SampleMarineSnowFogDensity(input.screenUV));

            half dither = (half)(ResolveTaaDitherPhaseNoise(input.screenUV) - 0.5) * saturate((half)_HectonNoirDepthFogParamsB.w) * 0.0039215686h;
            fogFactor = saturate(fogFactor + dither);

            half3 fogColor = (half3)lerp(
                _HectonNoirDepthFogShallowColor.rgb,
                _HectonNoirDepthFogAbyssColor.rgb,
                saturate(depthSq + depth01 * 0.18h));
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
