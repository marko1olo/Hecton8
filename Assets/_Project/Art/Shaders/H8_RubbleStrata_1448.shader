Shader "Hecton8/World/RubbleStrata1448"
{
    Properties
    {
        _Basalt ("Basalt", Color) = (0.25, 0.30, 0.27, 1.0)
        _Sand ("Sand Mineral", Color) = (0.56, 0.52, 0.38, 1.0)
        _Wet ("Wet Sheen", Color) = (0.42, 0.62, 0.56, 1.0)
        _StrataScale ("Strata Scale", Range(0.05, 3.0)) = 0.42
        _Contrast ("Contrast", Range(0.0, 2.0)) = 0.75
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Basalt;
                half4 _Sand;
                half4 _Wet;
                half _StrataScale;
                half _Contrast;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 573.73));
                p += dot(p, p + 29.17);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                float3 p = input.positionWS;
                half strata = 0.5h + 0.5h * sin((p.y * 3.1h + p.x * 0.17h + p.z * 0.11h) / max(_StrataScale, 0.05h));
                half grain = Hash21(floor(p.xz * 1.7h));
                half mineral = saturate(strata * 0.58h + grain * 0.42h);
                mineral = smoothstep(0.22h, 0.86h, mineral);
                half up = saturate(n.y * 0.5h + 0.5h);
                half3 baseCol = lerp(_Basalt.rgb, _Sand.rgb, mineral);
                half3 wet = lerp(baseCol, _Wet.rgb, saturate((1.0h - up) * 0.22h + grain * 0.08h));
                half shade = 0.62h + up * 0.24h + strata * 0.12h;
                half3 col = wet * lerp(1.0h, shade, _Contrast);
                return half4(max(col, half3(0.075h, 0.09h, 0.08h)), 1.0h);
            }
            ENDHLSL
        }
    }
}
