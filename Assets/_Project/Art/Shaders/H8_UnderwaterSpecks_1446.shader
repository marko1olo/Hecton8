Shader "Hecton8/World/UnderwaterSpecks1446"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.55, 1.0, 0.92, 0.34)
        _Softness ("Softness", Range(0.25, 3.0)) = 1.15
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 d = input.uv * 2.0h - 1.0h;
                half radius = saturate(1.0h - dot(d, d));
                half alpha = pow(radius, _Softness) * _Tint.a * input.color.a;
                return half4(_Tint.rgb * (0.65h + input.color.rgb * 0.35h), alpha);
            }
            ENDHLSL
        }
    }
}
