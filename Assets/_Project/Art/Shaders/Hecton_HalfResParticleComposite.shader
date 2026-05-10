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
        CBUFFER_END

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_HectonHalfResParticlesTex);

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

        float HectonSampleSceneRawDepth(float2 uv)
        {
            return SampleSceneDepth(uv);
        }

        float HectonResolveDepthEdgeFade(float centerRawDepth)
        {
            float edge = abs(ddx(centerRawDepth)) + abs(ddy(centerRawDepth));
            return saturate(1.0 - edge * 192.0);
        }

        half4 HectonSampleParticlesDepthFake(float2 uv)
        {
            half4 particles = SAMPLE_TEXTURE2D_X(_HectonHalfResParticlesTex, sampler_LinearClamp, uv);
            float centerRawDepth = HectonSampleSceneRawDepth(uv);
            half edgeFade = (half)lerp(0.45, 1.0, HectonResolveDepthEdgeFade(centerRawDepth));
            particles.a *= edgeFade;
            return particles;
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
            half4 particles = HectonSampleParticlesDepthFake(input.screenUV);
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

    FallBack Off
}
