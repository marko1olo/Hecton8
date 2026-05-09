Shader "Hidden/Hecton8/SargassumCutMaskStamp"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Stamp"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _StampUvRadiusStrength;
            float4 _ScrollUvOffset;
            float _Recovery;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                current = max(0.0h, current - _Recovery);

                half2 delta = input.uv - _StampUvRadiusStrength.xy;
                half radius = max(_StampUvRadiusStrength.z, 0.0001h);
                half normalized = saturate(1.0h - dot(delta, delta) / (radius * radius));
                half stamp = normalized * normalized * _StampUvRadiusStrength.w;
                current = max(current, stamp);

                return half4(current, current, current, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScrollCopy"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment ScrollFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _ScrollUvOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 ScrollFrag(Varyings input) : SV_Target
            {
                float2 sourceUv = input.uv + _ScrollUvOffset.xy;
                if (sourceUv.x < 0.0 || sourceUv.x > 1.0 || sourceUv.y < 0.0 || sourceUv.y > 1.0)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                half current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUv).r;
                return half4(current, current, current, 1.0h);
            }
            ENDHLSL
        }
    }
}
