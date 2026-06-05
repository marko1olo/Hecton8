Shader "Hecton8/World/UnderwaterHazeCurtain1454"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.09, 0.38, 0.40, 0.68)
        _BottomColor ("Bottom Color", Color) = (0.18, 0.55, 0.48, 0.36)
        _Softness ("Softness", Range(0.2, 4.0)) = 1.35
        _CausticColor ("Surface Caustic Color", Color) = (0.64, 1.0, 0.84, 0.32)
        _CausticScale ("Caustic Scale", Range(0.05, 4.0)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+24"
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
                half4 _TopColor;
                half4 _BottomColor;
                half4 _CausticColor;
                half _Softness;
                half _CausticScale;
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
                half v = saturate(input.uv.y);
                half edge = smoothstep(0.0h, 0.16h, input.uv.x) * smoothstep(1.0h, 0.84h, input.uv.x);
                half band = pow(saturate(1.0h - abs(v - 0.72h) * 1.9h), _Softness);
                half shimmer = pow(saturate(0.5h + 0.5h * sin(input.positionWS.x * _CausticScale + input.positionWS.y * 1.7h)), 6.0h);
                half4 col = lerp(_BottomColor, _TopColor, v);
                col.rgb += _CausticColor.rgb * shimmer * _CausticColor.a * band;
                col.a *= edge * input.color.a * saturate(0.35h + band);
                return col;
            }
            ENDHLSL
        }
    }
}
