Shader "Hidden/Hecton8/SedimentCapture"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "SedimentCapture"
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _HectonSedimentCaptureParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, float4(1.0, 0.0, 0.0, 1.0));
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float normalizedHeight = saturate((input.positionWS.y - _HectonSedimentCaptureParams.x) * _HectonSedimentCaptureParams.y);
                float upFacing = saturate((NormalizeNormalPerPixel(input.normalWS).y - _HectonSedimentCaptureParams.z) * _HectonSedimentCaptureParams.w);
                return half4(normalizedHeight, upFacing, 0.0h, 1.0h);
            }
            ENDHLSL
        }
    }
}
