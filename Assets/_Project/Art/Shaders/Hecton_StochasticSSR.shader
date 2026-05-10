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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _HectonSsrInputSize; // xy=input pixels, zw=input texel size
            float4 _HectonSsrParamsA; // x=max pixel offset, y=depth fade meters, z=intensity, w=edge fade
            float4 _HectonSsrParamsB; // x=noise modulation, y/z/w=reserved
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);

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

        float ResolveInterleavedNoise(float2 screenUV)
        {
            float2 pixel = floor(screenUV * _HectonSsrInputSize.xy);
            return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            float rawDepth = SampleSceneDepth(input.screenUV);
            if (ResolveRawDepthValidity(rawDepth) <= 0.5)
                return sourceColor;

            half3 normalWS = (half3)SampleSceneNormals(input.screenUV);
            half horizonMask = saturate(1.0h - abs(normalWS.y));
            horizonMask *= horizonMask;

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            half depthMask = (half)saturate(1.0 - linearEyeDepth * rcp(max(_HectonSsrParamsA.y, 1.0)));

            float2 edgeDistance = min(input.screenUV, 1.0 - input.screenUV);
            half edgeMask = (half)saturate(min(edgeDistance.x, edgeDistance.y) * max(_HectonSsrParamsA.w, 1.0));

            half noise = (half)ResolveInterleavedNoise(input.screenUV);
            half noiseMask = lerp(1.0h, lerp(0.72h, 1.0h, noise), saturate((half)_HectonSsrParamsB.x));

            float2 normalOffset = float2(normalWS.x, normalWS.z);
            float2 jitterOffset = float2(noise - 0.5h, 0.5h - noise) * 0.25h;
            float2 reflectionUV = saturate(input.screenUV + (normalOffset + jitterOffset) * (_HectonSsrParamsA.x * _HectonSsrInputSize.zw));
            half3 reflectedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, reflectionUV).rgb;

            half reflectionWeight = saturate((half)_HectonSsrParamsA.z * horizonMask * depthMask * edgeMask * noiseMask);
            sourceColor.rgb = lerp(sourceColor.rgb, reflectedColor, reflectionWeight);
            return sourceColor;
        }
        ENDHLSL

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
