Shader "HECTON/Sky/Hecton_SurfaceCloudDeck_1428"
{
    Properties
    {
        _BaseMap ("Cloud Density", 2D) = "white" {}
        [HDR] _Tint ("Cloud Tint", Color) = (0.72, 0.82, 0.90, 0.34)
        _Cutoff ("Density Cutoff", Range(0, 1)) = 0.32
        _Softness ("Density Softness", Range(0.01, 1)) = 0.34
        _Opacity ("Opacity", Range(0, 1)) = 0.34
        _Scroll ("Scroll", Vector) = (0.003, 0.0008, 0, 0)
        _HorizonFade ("Horizon Fade", Range(0, 1)) = 0.18
        _EdgeFade ("Edge Fade", Range(0, 0.5)) = 0.12
        _TopFade ("Top Fade", Range(0, 0.5)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent-80"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Cutoff;
                half _Softness;
                half _Opacity;
                float4 _Scroll;
                half _HorizonFade;
                half _EdgeFade;
                half _TopFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localUv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.localUv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv + _Scroll.xy * _Time.y;
                half density = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).r;
                half cloud = smoothstep(_Cutoff, saturate(_Cutoff + _Softness), density);
                half lowerGate = smoothstep(_HorizonFade, saturate(_HorizonFade + 0.22h), input.localUv.y);
                half upperGate = 1.0h - smoothstep(saturate(1.0h - _TopFade), 1.0h, input.localUv.y);
                half leftGate = smoothstep(0.0h, max(_EdgeFade, 0.001h), input.localUv.x);
                half rightGate = 1.0h - smoothstep(saturate(1.0h - _EdgeFade), 1.0h, input.localUv.x);
                half alpha = saturate(cloud * lowerGate * upperGate * leftGate * rightGate * _Opacity * _Tint.a);
                half3 color = _Tint.rgb * lerp(0.72h, 1.0h, cloud);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
