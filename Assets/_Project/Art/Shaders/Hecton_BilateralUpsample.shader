Shader "Hidden/Hecton8/BilateralUpsample"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "BilateralUpsample"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UberNoirReconstructionConstants)
                float4 _H8RenderScaleParams;
                float4 _H8TemporalParams;
                float4 _H8OverkillParams;
            CBUFFER_END

            float _H8UberNoirABSplit;

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            TEXTURE2D_X(_MotionVectorTexture);
            TEXTURE2D_X(_H8ReconstructionHistoryTex);

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
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = GetFullScreenTriangleTexCoord(input.vertexID);
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

            float3 Finite3(float3 value, float3 fallback)
            {
                return all(isfinite(value)) ? value : fallback;
            }

            float Finite01(float value)
            {
                return isfinite(value) ? saturate(value) : 0.0;
            }

            float SmoothRange01(float edge0, float edge1, float value)
            {
                float range = max(edge1 - edge0, 0.0001);
                float t = saturate((value - edge0) * rcp(range));
                return t * t * (3.0 - 2.0 * t);
            }

            float3 SampleColor(float2 uv)
            {
                return Finite3(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb, float3(0.0, 0.0, 0.0));
            }

            float Luma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float LinearDepthSafe(float2 uv)
            {
                float rawDepth = SampleSceneDepth(saturate(uv));
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
                return isfinite(depth) ? max(depth, 0.0) : 0.0;
            }

            float ReconstructionWeight(float3 sampleColor, float sampleDepth, float3 centerColor, float centerDepth, float2 offsetPixels)
            {
                float spatial = dot(offsetPixels, offsetPixels) * 0.18;
                float depthDiff = abs(sampleDepth - centerDepth) * lerp(0.28, 0.09, Finite01(_H8OverkillParams.w));
                float lumaDiff = abs(Luma(sampleColor) - Luma(centerColor)) * 18.0;
                return rcp(max(0.0001, 1.0 + spatial + depthDiff + lumaDiff));
            }

            float InterleavedGradientNoise(float2 uv)
            {
                float2 screenParams = max(_ScreenParams.xy, float2(1.0, 1.0));
                float2 pixel = floor(saturate(uv) * screenParams);
                pixel += frac(_Time.y * float2(59.0, 71.0));
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveTexel()
            {
                float2 texel = abs(_BlitTexture_TexelSize.xy);
                float2 fallback = rcp(max(_ScreenParams.xy, float2(1.0, 1.0)));
                return all(texel > 0.0) ? texel : fallback;
            }

            float3 ApplyNeighborhoodClamp(float3 value, float3 minColor, float3 maxColor)
            {
                return clamp(value, minColor - 0.025, maxColor + 0.025);
            }

            float3 ResolveTemporalHook(float2 uv, float3 current, float3 minColor, float3 maxColor)
            {
                float motionScale = Finite01(_H8TemporalParams.z);
                float historyWeight = Finite01(_H8TemporalParams.x) * motionScale;
                [branch]
                if (historyWeight <= 0.0001)
                    return current;

                float2 motion = SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_LinearClamp, saturate(uv)).rg;
                motion = all(isfinite(motion)) ? motion : float2(0.0, 0.0);
                float2 historyUv = saturate(uv + motion * motionScale);
                float3 history = Finite3(
                    SAMPLE_TEXTURE2D_X(_H8ReconstructionHistoryTex, sampler_LinearClamp, historyUv).rgb,
                    current);
                history = ApplyNeighborhoodClamp(history, minColor, maxColor);
                return lerp(current, history, historyWeight);
            }

            float3 ApplyDearLie(float2 uv, float3 color)
            {
                float2 centered = saturate(uv) - 0.5;
                float edge01 = saturate(dot(centered, centered) * 4.0);
                float grainStrength = max(0.0, _H8OverkillParams.x);
                float vignetteStrength = Finite01(_H8OverkillParams.y);
                float chromaStrength = max(0.0, _H8OverkillParams.z);
                float overkill01 = Finite01(_H8OverkillParams.w);

                [branch]
                if (chromaStrength > 0.00001)
                {
                    float2 caOffset = float2(edge01 * chromaStrength, 0.0);
                    float3 caColor = color;
                    caColor.r = SampleColor(uv + caOffset).r;
                    caColor.b = SampleColor(uv - caOffset).b;
                    color = lerp(color, caColor, saturate(edge01 * 0.92));
                }

                float luma = Luma(color);
                float grain = (InterleavedGradientNoise(uv) - 0.5) * grainStrength * (1.0 - saturate(luma));
                color += grain;
                color *= 1.0 - edge01 * vignetteStrength * 0.42;

                [branch]
                if (overkill01 > 0.001)
                {
                    float glintSeed = InterleavedGradientNoise(uv * 8.0 + _Time.yy * 0.07);
                    float glintMask = step(0.992 - overkill01 * 0.004, glintSeed) * edge01;
                    color += glintMask * overkill01 * float3(0.16, 0.21, 0.22);
                }

                return max(color, float3(0.0015, 0.0022, 0.0030));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ResolveXRStereoScreenUV(input.screenUV);
                uv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);
                float3 centerColor = SampleColor(uv);

                [branch]
                if (_H8UberNoirABSplit > 0.5 && uv.x < 0.5)
                    return half4((half3)centerColor, 1.0h);

                float safeScale = max(0.3, isfinite(_H8RenderScaleParams.x) ? _H8RenderScaleParams.x : 1.0);
                float inverseScale = max(1.0, isfinite(_H8RenderScaleParams.z) ? _H8RenderScaleParams.z : rcp(safeScale));
                float sharpness = Finite01(_H8RenderScaleParams.w);
                float radius = clamp(isfinite(_H8TemporalParams.w) ? _H8TemporalParams.w : 1.0, 0.25, 5.0);
                float2 texel = ResolveTexel() * radius * max(1.0, inverseScale * 0.5);

                float centerDepth = LinearDepthSafe(uv);
                float3 sum = centerColor;
                float weightSum = 1.0;
                float3 minColor = centerColor;
                float3 maxColor = centerColor;

                float2 offsets[4] = {
                    float2( 1.0,  0.0),
                    float2(-1.0,  0.0),
                    float2( 0.0,  1.0),
                    float2( 0.0, -1.0)
                };

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 tapUv = uv + texel * offsets[i];
                    float3 tapColor = SampleColor(tapUv);
                    float tapDepth = LinearDepthSafe(tapUv);
                    float weight = ReconstructionWeight(tapColor, tapDepth, centerColor, centerDepth, offsets[i]);
                    sum += tapColor * weight;
                    weightSum += weight;
                    minColor = min(minColor, tapColor);
                    maxColor = max(maxColor, tapColor);
                }

                float diagonalWeight01 = SmoothRange01(0.25, 0.85, Finite01(_H8OverkillParams.w));
                [branch]
                if (diagonalWeight01 > 0.001)
                {
                    float2 diagOffsets[4] = {
                        float2( 1.0,  1.0),
                        float2(-1.0,  1.0),
                        float2( 1.0, -1.0),
                        float2(-1.0, -1.0)
                    };

                    [unroll]
                    for (int d = 0; d < 4; d++)
                    {
                        float2 tapUv = uv + texel * diagOffsets[d];
                        float3 tapColor = SampleColor(tapUv);
                        float tapDepth = LinearDepthSafe(tapUv);
                        float weight = ReconstructionWeight(tapColor, tapDepth, centerColor, centerDepth, diagOffsets[d]) * (0.72 * diagonalWeight01);
                        sum += tapColor * weight;
                        weightSum += weight;
                        minColor = min(minColor, tapColor);
                        maxColor = max(maxColor, tapColor);
                    }
                }

                float3 bilateral = sum * rcp(max(weightSum, 0.0001));
                float variance = saturate(abs(Luma(centerColor) - Luma(bilateral)) * 10.0);
                float scaleDeficit01 = saturate(1.0 - safeScale);
                float ringingGuard = lerp(1.0, 0.42, SmoothRange01(0.24, 0.52, scaleDeficit01));
                float detailGain = sharpness * ringingGuard * lerp(0.35, 1.0, variance);
                float3 reconstructed = centerColor + (centerColor - bilateral) * detailGain;
                reconstructed = ApplyNeighborhoodClamp(reconstructed, minColor, maxColor);
                reconstructed = ResolveTemporalHook(uv, reconstructed, minColor, maxColor);
                float reconstructionDither = (InterleavedGradientNoise(uv) - 0.5) * scaleDeficit01 * (1.0 - ringingGuard) * 0.012;
                reconstructed = ApplyNeighborhoodClamp(reconstructed + reconstructionDither.xxx, minColor, maxColor);
                reconstructed = ApplyDearLie(uv, reconstructed);
                return half4((half3)reconstructed, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
