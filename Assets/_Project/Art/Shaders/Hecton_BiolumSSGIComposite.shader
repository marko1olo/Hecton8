Shader "Hidden/Hecton8/BiolumSSGIComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Composite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #ifndef UNITY_PASS_STEREO_INSTANCE_ID
            #define UNITY_PASS_STEREO_INSTANCE_ID(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            #endif

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_X(_HectonBiolumSSGITexture);

            half3 ResolveFiniteHalf3OrZero(half3 value)
            {
                return all(isfinite((float3)value)) ? value : half3(0.0h, 0.0h, 0.0h);
            }

            half3 ClampBiolumHdr(half3 value)
            {
                return min(max(ResolveFiniteHalf3OrZero(value), half3(0.0h, 0.0h, 0.0h)), half3(10.0h, 10.0h, 10.0h));
            }

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
#endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
#else
                return screenUV;
#endif
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                UNITY_PASS_STEREO_INSTANCE_ID(input);
                float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(screenUV));
                half4 giColor = SAMPLE_TEXTURE2D_X(_HectonBiolumSSGITexture, sampler_LinearClamp, screenUV);
                half giAlpha = saturate(isfinite((float)giColor.a) ? giColor.a : 0.0h);
                half3 composed = ClampBiolumHdr(sourceColor.rgb + ClampBiolumHdr(giColor.rgb) * giAlpha);
                half sourceAlpha = saturate(isfinite((float)sourceColor.a) ? sourceColor.a : 1.0h);
                return half4(composed, sourceAlpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ProxyComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #ifndef UNITY_PASS_STEREO_INSTANCE_ID
            #define UNITY_PASS_STEREO_INSTANCE_ID(input) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            #endif

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_X(_HectonSourceDepth);

            float4 _HectonSSGIInputSize;
            float _HectonSSGIThreshold;
            float _HectonSSGIIntensity;
            float _HectonSSGIRadius;
            float _HectonSSGIDepthSigma;
            int _HectonSSGISampleCount;

            static const float2 kProxyOffsets[8] =
            {
                float2(1.0, 0.0),
                float2(-1.0, 0.0),
                float2(0.0, 1.0),
                float2(0.0, -1.0),
                float2(1.0, 1.0),
                float2(-1.0, 1.0),
                float2(1.0, -1.0),
                float2(-1.0, -1.0)
            };

            half3 ResolveFiniteHalf3OrZero(half3 value)
            {
                return all(isfinite((float3)value)) ? value : half3(0.0h, 0.0h, 0.0h);
            }

            half3 ClampBiolumHdr(half3 value)
            {
                return min(max(ResolveFiniteHalf3OrZero(value), half3(0.0h, 0.0h, 0.0h)), half3(10.0h, 10.0h, 10.0h));
            }

            half Luminance(half3 color)
            {
                return dot(ResolveFiniteHalf3OrZero(color), half3(0.2126h, 0.7152h, 0.0722h));
            }

            float ResolveDepthValidMask(float rawDepth)
            {
                float finiteMask = isfinite(rawDepth) ? 1.0 : 0.0;
#if defined(UNITY_REVERSED_Z)
                return finiteMask * step(0.0001, rawDepth);
#else
                return finiteMask * step(rawDepth, 0.9999);
#endif
            }

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
#endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
#else
                return screenUV;
#endif
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                UNITY_PASS_STEREO_INSTANCE_ID(input);

                float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
                float2 sourceUV = ResolveFoveatedSourceUV(screenUV);
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUV);
                float centerDepth = SAMPLE_TEXTURE2D_X(_HectonSourceDepth, sampler_PointClamp, sourceUV).r;
                if (ResolveDepthValidMask(centerDepth) <= 0.5)
                    return sourceColor;

                int sampleCount = clamp(_HectonSSGISampleCount, 1, 8);
                float threshold = max(0.0, isfinite(_HectonSSGIThreshold) ? _HectonSSGIThreshold : 1.0);
                float intensity = max(0.0, isfinite(_HectonSSGIIntensity) ? _HectonSSGIIntensity : 0.0);
                float radius = max(1.0, isfinite(_HectonSSGIRadius) ? _HectonSSGIRadius : 1.0);
                float depthSigma = max(0.01, isfinite(_HectonSSGIDepthSigma) ? _HectonSSGIDepthSigma : 1.0);
                float2 texelRadius = max(_HectonSSGIInputSize.zw, float2(0.00001, 0.00001)) * radius;

                half3 accumulated = half3(0.0h, 0.0h, 0.0h);
                half weightSum = 0.0h;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float sampleActive = step((float)i + 0.5, (float)sampleCount);
                    float2 sampleUV = ResolveFoveatedSourceUV(screenUV + kProxyOffsets[i] * texelRadius);
                    half4 sampleColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                    float sampleDepth = SAMPLE_TEXTURE2D_X(_HectonSourceDepth, sampler_PointClamp, sampleUV).r;
                    float depthValid = ResolveDepthValidMask(sampleDepth);
                    half emissionMask = saturate((Luminance(sampleColor.rgb) - (half)threshold) * 0.75h);
                    float depthWeight = rcp(max(1.0 + abs(sampleDepth - centerDepth) * depthSigma, 0.001));
                    float spatialWeight = saturate(1.0 - dot(kProxyOffsets[i], kProxyOffsets[i]) * 0.19);
                    half weight = (half)(sampleActive * depthValid * depthWeight * spatialWeight) * emissionMask;
                    accumulated += ClampBiolumHdr(sampleColor.rgb) * weight;
                    weightSum += weight;
                }

                half confidence = saturate(weightSum);
                half3 proxyBleed = weightSum > 0.0001h ? accumulated * rcp(max(weightSum, 0.0001h)) : half3(0.0h, 0.0h, 0.0h);
                half3 composed = ClampBiolumHdr(sourceColor.rgb + proxyBleed * (half)intensity * confidence);
                half sourceAlpha = saturate(isfinite((float)sourceColor.a) ? sourceColor.a : 1.0h);
                return half4(composed, sourceAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
