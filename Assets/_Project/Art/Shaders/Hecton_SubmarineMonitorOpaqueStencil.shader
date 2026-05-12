Shader "Hecton8/Submarine/MonitorOpaqueStencil"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "black" {}
        _BaseColor ("Base Color", Color) = (0.08, 0.96, 0.82, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.25
        _StencilRef ("Stencil Reference", Float) = 8
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilComp ("Stencil Comparison", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+80"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite On
        ZTest LEqual
        Blend One Zero
        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            Comp [_StencilComp]
            Pass Keep
        }

        Pass
        {
            Name "MonitorOpaqueStencil"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _AlphaCutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _SubInteriorLightingState;

            float LowPowerFlicker01(float2 uv)
            {
                float lowPower = saturate((0.15 - _SubInteriorLightingState.z) * 6.666667);
                float scanNoise = frac(uv.y * 41.0 + uv.x * 13.0 + _Time.y * 29.0);
                return lerp(1.0, 0.62 + scanNoise * 0.38, lowPower);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = sample.a * (half)_BaseColor.a;
                clip(alpha - (half)_AlphaCutoff);
                half flicker = (half)LowPowerFlicker01(input.uv);
                return half4(sample.rgb * (half3)_BaseColor.rgb * flicker, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
