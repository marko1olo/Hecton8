Shader "HECTON/UI/FabricatorHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.88, 1.0, 0.42)
        _ScanlineDensity ("Scanline Density", Range(1, 64)) = 18
        _ScanlineSpeed ("Scanline Speed", Range(0, 16)) = 4
        _ScanlineEmission ("Scanline Emission", Range(0, 4)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineEmission;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDirection)), 2.4);
                float pulse = 0.7 + 0.3 * sin(_Time.y * 6.0 + input.positionWS.y * 8.0);
                float scanline = frac(input.positionOS.y * _ScanlineDensity + _Time.y * _ScanlineSpeed);
                float scanlineBand = 1.0 - abs(scanline * 2.0 - 1.0);
                float scanlineGlow = pow(saturate(scanlineBand), 6.0) * _ScanlineEmission;
                half alpha = saturate(_BaseColor.a + fresnel * 0.45 + scanlineGlow * 0.18) * pulse;
                half3 color = (_BaseColor.rgb * (0.85 + fresnel * 0.75)) + (_BaseColor.rgb * scanlineGlow);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
