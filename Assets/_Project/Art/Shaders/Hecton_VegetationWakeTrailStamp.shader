Shader "Hidden/Hecton8/VegetationWakeTrailStamp"
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

            float4 _StampUvEllipse;
            float4 _StampDirectionStrength;
            float _Fade;

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
                half4 current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half currentIntensity = max(0.0h, current.b - _Fade);
                half2 currentDirection = current.rg * 2.0h - 1.0h;

                half2 stampDirection = _StampDirectionStrength.xy;
                half stampDirectionLen = length(stampDirection);
                stampDirection = stampDirectionLen > 0.0001h ? stampDirection / stampDirectionLen : half2(0.0h, 1.0h);
                half2 stampBitangent = half2(-stampDirection.y, stampDirection.x);

                half2 delta = input.uv - _StampUvEllipse.xy;
                half radius = max(_StampUvEllipse.z, 0.0001h);
                half halfLength = max(_StampUvEllipse.w, 0.0001h);
                half verticalImpulse = saturate(_StampDirectionStrength.w);
                half along = dot(delta, stampDirection) / halfLength;
                half across = dot(delta, stampBitangent) / radius;
                half shape = saturate(1.0h - sqrt(along * along + across * across));
                half stampIntensity = shape * shape * saturate(_StampDirectionStrength.z) * lerp(1.0h, 1.55h, verticalImpulse);

                half blendWeight = currentIntensity + stampIntensity;
                half2 blendedDirection = blendWeight > 0.0001h
                    ? normalize(currentDirection * currentIntensity + stampDirection * stampIntensity)
                    : stampDirection;
                blendedDirection = normalize(blendedDirection + stampBitangent * (across * verticalImpulse * 0.35h));
                half finalIntensity = max(currentIntensity, stampIntensity);
                half previousWaveState = lerp(finalIntensity, finalIntensity * 0.08h, verticalImpulse);
                half2 encodedDirection = blendedDirection * 0.5h + 0.5h;

                return half4(encodedDirection, finalIntensity, previousWaveState);
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
                    return half4(0.5h, 0.5h, 0.0h, 0.0h);

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUv);
            }
            ENDHLSL
        }
    }
}
