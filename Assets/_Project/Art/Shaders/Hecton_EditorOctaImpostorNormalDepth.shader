Shader "Hidden/Hecton8/Editor/OctahedralImpostorNormalDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Pass
        {
            Name "NormalDepth"
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float normalLengthSq = dot(input.normalWS, input.normalWS);
                float3 normalWS = normalLengthSq > 0.000001 ? input.normalWS * rsqrt(normalLengthSq) : float3(0.0, 1.0, 0.0);
                half3 normal = (half3)(normalWS * 0.5 + 0.5);
                half depth = (half)saturate(Linear01Depth(input.positionCS.z, _ZBufferParams));
                return half4(normal, depth);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
