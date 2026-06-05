Shader "Hecton8/World/UnderwaterSurfaceSheet1455"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.20, 0.70, 0.66, 0.52)
        _BrightColor ("Bright Surface Color", Color) = (0.78, 1.0, 0.88, 0.34)
        _FoamColor ("Soft Foam Color", Color) = (0.86, 1.0, 0.92, 0.28)
        _SwellScale ("Swell Scale", Range(0.02, 2.0)) = 0.18
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.56
        _SeaLevel ("Sea Level", Float) = 14.02
        _CameraFadeSharpness ("Camera Fade Sharpness", Range(0.5, 16.0)) = 7.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+45"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _BrightColor;
                half4 _FoamColor;
                half _SwellScale;
                half _Opacity;
                half _SeaLevel;
                half _CameraFadeSharpness;
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
                half swellA = 0.5h + 0.5h * sin(p.x * _SwellScale + p.y * 0.047h);
                half swellB = 0.5h + 0.5h * sin(p.y * (_SwellScale * 1.47h) - p.x * 0.036h + 1.9h);
                half shimmer = pow(saturate(swellA * swellB), 4.5h);
                half foam = pow(saturate(0.5h + 0.5h * sin((p.x + p.y) * 0.19h)), 10.0h);
                half edge = smoothstep(0.0h, 0.08h, input.uv.x) * smoothstep(1.0h, 0.92h, input.uv.x) *
                            smoothstep(0.0h, 0.08h, input.uv.y) * smoothstep(1.0h, 0.92h, input.uv.y);
                half3 col = _WaterColor.rgb;
                col = lerp(col, _BrightColor.rgb, shimmer * _BrightColor.a);
                col += _FoamColor.rgb * foam * _FoamColor.a;
                half underwaterOnly = saturate((_SeaLevel - _WorldSpaceCameraPos.y) * _CameraFadeSharpness);
                half alpha = _WaterColor.a * _Opacity * edge * input.color.a * underwaterOnly;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
