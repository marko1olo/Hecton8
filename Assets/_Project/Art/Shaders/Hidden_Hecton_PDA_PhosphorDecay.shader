Shader "Hidden/Hecton8/PDA Phosphor Decay"
{
    Properties
    {
        _PreviousTex ("Previous", 2D) = "black" {}
        _CurrentTex ("Current", 2D) = "black" {}
        _Decay ("Decay", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "PhosphorDecay"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_PreviousTex);
            SAMPLER(sampler_PreviousTex);
            TEXTURE2D(_CurrentTex);
            SAMPLER(sampler_CurrentTex);

            CBUFFER_START(UnityPerMaterial)
                float _Decay;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.uv * 2.0 - 1.0, 0.0, 1.0);
                output.positionCS.y *= -1.0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 previousSample = SAMPLE_TEXTURE2D(_PreviousTex, sampler_PreviousTex, input.uv);
                half4 currentSample = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, input.uv);
                half4 decayed = previousSample * saturate(_Decay);
                half4 resolved = max(decayed, currentSample);
                resolved.rgb += currentSample.rgb;
                resolved.a = saturate(max(decayed.a, currentSample.a));
                return saturate(resolved);
            }
            ENDHLSL
        }
    }
}
