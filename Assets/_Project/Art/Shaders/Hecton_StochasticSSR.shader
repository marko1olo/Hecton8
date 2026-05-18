Shader "Hidden/Hecton8/StochasticSSR"
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

        CBUFFER_START(HectonStochasticSsrGlobals)
            float4 _HectonSsrInputSize; // xy=input pixels, zw=input texel size
            float4 _HectonSsrParamsA; // x=max pixel offset, y=depth fade meters, z=intensity, w=edge fade
            float4 _HectonSsrParamsB; // x=noise modulation, y/z/w=reserved
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonSsrMaskTex);

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
            float2 pixel = floor(screenUV * _HectonSsrInputSize.xy);
            uint2 pixelParity = (uint2)pixel & 1u;
            uint phaseIndex = pixelParity.x | (pixelParity.y << 1u);
            float2 taaPhase = float2((float)(phaseIndex & 1u), (float)((phaseIndex >> 1u) & 1u)) * 0.5;
            return frac(52.9829189 * frac(dot(pixel + taaPhase, float2(0.06711056, 0.00583715))));
        }

        half4 FragMask(Varyings input) : SV_Target
        {
            float rawDepth = SampleSceneDepth(input.screenUV);
            if (ResolveRawDepthValidity(rawDepth) <= 0.5)
                return half4(0.0h, 0.0h, 0.0h, 1.0h);

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            half depthMask = (half)saturate(1.0 - linearEyeDepth * rcp(max(_HectonSsrParamsA.y, 1.0)));

            float2 edgeDistance = min(input.screenUV, 1.0 - input.screenUV);
            half edgeMask = (half)saturate(min(edgeDistance.x, edgeDistance.y) * max(_HectonSsrParamsA.w, 1.0));

            half noise = (half)ResolveTaaDitherPhaseNoise(input.screenUV);
            half noiseMask = lerp(1.0h, lerp(0.72h, 1.0h, noise), saturate((half)_HectonSsrParamsB.x));
            half screenSheenMask = (half)saturate((input.screenUV.y - 0.36) * 1.85);
            half reflectionWeight = saturate((half)_HectonSsrParamsA.z * screenSheenMask * depthMask * edgeMask * noiseMask);
            return half4(reflectionWeight, reflectionWeight, reflectionWeight, 1.0h);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            half reflectionWeight = SAMPLE_TEXTURE2D_X(_HectonSsrMaskTex, sampler_LinearClamp, input.screenUV).r;
            if (reflectionWeight <= 0.0001h)
                return sourceColor;

            half staticSeed = reflectionWeight - 0.5h;
            float2 staticOffset = float2(staticSeed, -staticSeed) * 0.25h;
            float2 reflectionUV = saturate(input.screenUV + (float2(0.0, -1.0) + staticOffset) * (_HectonSsrParamsA.x * _HectonSsrInputSize.zw));
            half3 reflectedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, reflectionUV).rgb;

            sourceColor.rgb = lerp(sourceColor.rgb, reflectedColor, reflectionWeight);
            return sourceColor;
        }
        ENDHLSL

        Pass
        {
            Name "Mask"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }

    FallBack Off
}
