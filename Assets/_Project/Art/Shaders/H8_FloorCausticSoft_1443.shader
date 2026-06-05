Shader "Hecton8/World/FloorCausticSoft1443"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.70, 1.0, 0.86, 0.28)
        _ScaleA ("Scale A", Range(0.05, 4.0)) = 0.68
        _ScaleB ("Scale B", Range(0.05, 4.0)) = 1.12
        _Sharpness ("Sharpness", Range(1.0, 12.0)) = 5.2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+18"
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
                half _ScaleA;
                half _ScaleB;
                half _Sharpness;
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
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                half a = 0.5h + 0.5h * sin((p.x * _ScaleA) + (p.y * 0.37h));
                half b = 0.5h + 0.5h * sin((p.y * _ScaleB) - (p.x * 0.29h) + 1.7h);
                half c = 0.5h + 0.5h * sin((p.x + p.y) * 0.43h);
                half caustic = pow(saturate(a * b * 0.78h + c * 0.22h), _Sharpness);
                half edge = saturate(input.color.a);
                half alpha = caustic * edge * _Tint.a;
                return half4(_Tint.rgb * (0.72h + caustic * 0.48h), alpha);
            }
            ENDHLSL
        }
    }
}
