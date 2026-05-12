Shader "Hidden/Hecton8/HalfResParticleComposite"
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
            float _HectonHalfResParticlesCompositeStrength;
            float _HectonHalfResParticlesBilateralDepthScale;
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonHalfResParticlesTex);
        float4 _HectonHalfResParticlesTex_TexelSize;

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

        float HectonSampleSceneEyeDepth(float2 uv)
        {
            return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        }

        float HectonBilateralDepthWeight(float centerDepth, float tapDepth)
        {
            float depthScale = max(_HectonHalfResParticlesBilateralDepthScale, 0.001);
            return exp2(-abs(tapDepth - centerDepth) * depthScale);
        }

        void HectonAccumulateParticleTap(float2 uv, float centerDepth, inout float4 colorAccum, inout float weightAccum)
        {
            float2 clampedUv = saturate(uv);
            float tapDepth = HectonSampleSceneEyeDepth(clampedUv);
            float weight = HectonBilateralDepthWeight(centerDepth, tapDepth);
            colorAccum += (float4)SAMPLE_TEXTURE2D_X(_HectonHalfResParticlesTex, sampler_LinearClamp, clampedUv) * weight;
            weightAccum += weight;
        }

        half4 HectonSampleParticlesBilateral(float2 uv)
        {
            float2 fallbackTexel = rcp(max(_ScaledScreenParams.xy, float2(1.0, 1.0)));
            float2 halfResTexel = max(_HectonHalfResParticlesTex_TexelSize.xy, fallbackTexel);
            float2 tapOffset = halfResTexel * 0.5;
            float centerDepth = HectonSampleSceneEyeDepth(uv);
            float4 colorAccum = 0.0;
            float weightAccum = 0.0;

            HectonAccumulateParticleTap(uv + tapOffset * float2(-1.0, -1.0), centerDepth, colorAccum, weightAccum);
            HectonAccumulateParticleTap(uv + tapOffset * float2( 1.0, -1.0), centerDepth, colorAccum, weightAccum);
            HectonAccumulateParticleTap(uv + tapOffset * float2(-1.0,  1.0), centerDepth, colorAccum, weightAccum);
            HectonAccumulateParticleTap(uv + tapOffset * float2( 1.0,  1.0), centerDepth, colorAccum, weightAccum);

            return (half4)(colorAccum * rcp(max(weightAccum, 0.0001)));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            half4 particles = HectonSampleParticlesBilateral(input.screenUV);
            half strength = saturate((half)_HectonHalfResParticlesCompositeStrength);
            half alpha = saturate(particles.a * strength);
            sourceColor.rgb = sourceColor.rgb * (1.0h - alpha) + particles.rgb * strength;
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

    FallBack "Hidden/Hecton8/InternalBlackError"
}
