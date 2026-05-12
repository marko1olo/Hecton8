Shader "Hecton8/Submarine/CockpitGlassStencil"
{
    Properties
    {
        _MaskMap ("Mask Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _StencilRef ("Stencil Reference", Float) = 8
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest+40"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite Off
        ZTest LEqual
        ColorMask 0

        Pass
        {
            Name "CockpitGlassStencil"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
                WriteMask [_StencilWriteMask]
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskMap_ST;
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MaskMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).a;
                clip(alpha - (half)_Cutoff);
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
