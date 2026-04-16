Shader "HECTON/VFX/LeakPlume"
{
    Properties
    {
        [MainTexture] _MainTex ("Plume Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (0.64, 0.76, 0.8, 0.18)
        _Opacity ("Opacity", Range(0, 2)) = 1
        _LuminanceBias ("Luminance Bias", Range(0, 1)) = 0.08
        _LuminancePower ("Luminance Power", Range(0.25, 4)) = 1.35
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LeakPlume"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _Opacity;
                half _LuminanceBias;
                half _LuminancePower;
                half _EdgeSoftness;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 plumeSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(plumeSample.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half derivedMask = saturate((luminance - _LuminanceBias) / max(_EdgeSoftness, 0.001h));
                derivedMask = pow(derivedMask, max(_LuminancePower, 0.001h));

                half3 color = plumeSample.rgb * _TintColor.rgb * input.color.rgb;
                half alpha = saturate(derivedMask * _TintColor.a * input.color.a * _Opacity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
