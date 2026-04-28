Shader "HECTON/UI/FabricatorHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 0.88, 1.0, 0.42)
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDirection)), 2.4);
                float pulse = 0.7 + 0.3 * sin(_Time.y * 6.0 + input.positionWS.y * 8.0);
                half alpha = saturate(_BaseColor.a + fresnel * 0.45) * pulse;
                half3 color = _BaseColor.rgb * (0.85 + fresnel * 0.75);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
