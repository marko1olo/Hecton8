Shader "Hidden/Hecton8/VisorAR"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+90"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "VisorARCopy"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CopyVert
            #pragma fragment CopyFrag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);

            struct CopyAttributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct CopyVaryings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            CopyVaryings CopyVert(CopyAttributes input)
            {
                CopyVaryings output;
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

            float2 CopyResolveStereoUV(float2 uv)
            {
            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(uv);
            #else
                return uv;
            #endif
            }

            half4 CopyFrag(CopyVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = CopyResolveStereoUV(input.screenUV);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VisorARStencilResolve"

            Stencil
            {
                Ref 1
                Comp Equal
                Pass Keep
                ReadMask 1
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define HECTON_VISOR_MAX_TARGETS 16

            CBUFFER_START(HectonVisorHudParams)
                float4 _HectonVisorTargetCoordinates;
                float4 _HectonVisorVitalStats;
                float4 _HectonVisorGlitchParams;
                float4 _HectonVisorQualityAndTime;
            CBUFFER_END

            CBUFFER_START(HectonVisorDigitParams)
                float4 _HectonVisorOxygenDigits;
                float4 _HectonVisorDepthDigits;
                float4 _HectonVisorPressureDigits;
                float4 _HectonVisorWarningDigits;
            CBUFFER_END

            struct VisorArTargetDTO
            {
                float4 ScreenAndFlags;
                float4 ColorAndPulse;
                float4 LocalMetersAndDistance;
                float4 ShapeParams;
            };

            StructuredBuffer<VisorArTargetDTO> _HectonVisorArTargets;

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;

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

            float2 ResolveStereoUV(float2 uv)
            {
            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(uv);
            #else
                return uv;
            #endif
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float SegmentBox(float2 uv, float2 center, float2 halfSize)
            {
                float2 d = abs(uv - center) - halfSize;
                float outside = length(max(d, 0.0));
                float inside = min(max(d.x, d.y), 0.0);
                return 1.0 - smoothstep(0.0, 0.025, outside + inside);
            }

            float DigitSegmentMask(float digit, float segment)
            {
                float d = floor(digit + 0.5);
                float active = 0.0;
                active += step(abs(d - 0.0), 0.1) * (1.0 - step(abs(segment - 6.0), 0.1));
                active += step(abs(d - 1.0), 0.1) * (step(abs(segment - 1.0), 0.1) + step(abs(segment - 2.0), 0.1));
                active += step(abs(d - 2.0), 0.1) * (step(abs(segment - 0.0), 0.1) + step(abs(segment - 1.0), 0.1) + step(abs(segment - 6.0), 0.1) + step(abs(segment - 4.0), 0.1) + step(abs(segment - 3.0), 0.1));
                active += step(abs(d - 3.0), 0.1) * (step(abs(segment - 0.0), 0.1) + step(abs(segment - 1.0), 0.1) + step(abs(segment - 6.0), 0.1) + step(abs(segment - 2.0), 0.1) + step(abs(segment - 3.0), 0.1));
                active += step(abs(d - 4.0), 0.1) * (step(abs(segment - 5.0), 0.1) + step(abs(segment - 6.0), 0.1) + step(abs(segment - 1.0), 0.1) + step(abs(segment - 2.0), 0.1));
                active += step(abs(d - 5.0), 0.1) * (step(abs(segment - 0.0), 0.1) + step(abs(segment - 5.0), 0.1) + step(abs(segment - 6.0), 0.1) + step(abs(segment - 2.0), 0.1) + step(abs(segment - 3.0), 0.1));
                active += step(abs(d - 6.0), 0.1) * (step(abs(segment - 0.0), 0.1) + step(abs(segment - 5.0), 0.1) + step(abs(segment - 6.0), 0.1) + step(abs(segment - 4.0), 0.1) + step(abs(segment - 2.0), 0.1) + step(abs(segment - 3.0), 0.1));
                active += step(abs(d - 7.0), 0.1) * (step(abs(segment - 0.0), 0.1) + step(abs(segment - 1.0), 0.1) + step(abs(segment - 2.0), 0.1));
                active += step(abs(d - 8.0), 0.1);
                active += step(abs(d - 9.0), 0.1) * (1.0 - step(abs(segment - 4.0), 0.1));
                return saturate(active);
            }

            float DrawDigit(float2 uv, float digit)
            {
                float visible = step(-0.5, digit);
                float mask = 0.0;
                mask += SegmentBox(uv, float2(0.5, 0.91), float2(0.28, 0.045)) * DigitSegmentMask(digit, 0.0);
                mask += SegmentBox(uv, float2(0.82, 0.68), float2(0.045, 0.23)) * DigitSegmentMask(digit, 1.0);
                mask += SegmentBox(uv, float2(0.82, 0.28), float2(0.045, 0.23)) * DigitSegmentMask(digit, 2.0);
                mask += SegmentBox(uv, float2(0.5, 0.08), float2(0.28, 0.045)) * DigitSegmentMask(digit, 3.0);
                mask += SegmentBox(uv, float2(0.18, 0.28), float2(0.045, 0.23)) * DigitSegmentMask(digit, 4.0);
                mask += SegmentBox(uv, float2(0.18, 0.68), float2(0.045, 0.23)) * DigitSegmentMask(digit, 5.0);
                mask += SegmentBox(uv, float2(0.5, 0.50), float2(0.28, 0.045)) * DigitSegmentMask(digit, 6.0);
                return saturate(mask * visible);
            }

            float DrawDigitRun(float2 uv, float2 origin, float scale, float4 digits)
            {
                float2 local = (uv - origin) / max(scale, 0.0001);
                float digitIndex = floor(local.x);
                float2 digitUv = float2(frac(local.x), local.y);
                float mask = 0.0;
                mask += DrawDigit(digitUv, digits.x) * step(abs(digitIndex - 0.0), 0.1);
                mask += DrawDigit(digitUv, digits.y) * step(abs(digitIndex - 1.0), 0.1);
                mask += DrawDigit(digitUv, digits.z) * step(abs(digitIndex - 2.0), 0.1);
                mask += DrawDigit(digitUv, digits.w) * step(abs(digitIndex - 3.0), 0.1);
                return saturate(mask);
            }

            float LineBox(float2 uv, float2 center, float2 halfSize, float softness)
            {
                float2 d = abs(uv - center) - halfSize;
                float dist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
                return 1.0 - smoothstep(0.0, softness, dist);
            }

            float DrawBracket(float2 uv, float2 center, float scale, float edge01)
            {
                float2 p = (uv - center) / max(scale, 0.0001);
                float2 ap = abs(p);
                float corner = smoothstep(0.68, 0.58, max(ap.x, ap.y));
                float outer = 1.0 - smoothstep(0.78, 0.9, max(ap.x, ap.y));
                float horizontal = LineBox(ap, float2(0.53, 0.66), float2(0.18, 0.025), 0.018);
                float vertical = LineBox(ap, float2(0.66, 0.53), float2(0.025, 0.18), 0.018);
                float reticle = LineBox(p, float2(0.0, 0.0), float2(0.11, 0.008), 0.01) + LineBox(p, float2(0.0, 0.0), float2(0.008, 0.11), 0.01);
                return saturate((horizontal + vertical) * outer + reticle * lerp(0.2, 0.9, saturate(1.0 - edge01)) + corner * 0.03);
            }

            float DrawScanline(float2 uv, float time, float quality)
            {
                float y = uv.y * lerp(240.0, 720.0, quality) + time * lerp(12.0, 42.0, quality);
                return 0.5 + 0.5 * sin(y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ResolveStereoUV(input.screenUV);
                float quality = saturate(_HectonVisorQualityAndTime.x);
                float time = _HectonVisorQualityAndTime.y;
                float fontScale = max(0.01, _HectonVisorQualityAndTime.w);
                float stress = saturate(_HectonVisorGlitchParams.x);
                float fogIntensity = saturate(_HectonVisorGlitchParams.y);
                float curvature = saturate(_HectonVisorGlitchParams.z);

                float2 centered = uv * 2.0 - 1.0;
                float radial = dot(centered, centered);
                float2 curvedUv = uv + centered * radial * curvature * lerp(0.002, 0.024, quality);
                float chromaWeight = smoothstep(0.06, 1.0, quality);
                float chroma = lerp(0.0002, 0.0025, chromaWeight) * (0.25 + stress);
                half3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(curvedUv)).rgb;
                [branch]
                if (chromaWeight > 0.0001)
                {
                    half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(curvedUv + float2(chroma, 0.0))).r;
                    half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(curvedUv - float2(chroma, 0.0))).b;
                    source = lerp(source, half3(red, source.g, blue), chromaWeight);
                }

                float3 hudColor = float3(0.42, 0.94, 0.98);
                float3 warnColor = float3(1.0, 0.33, 0.18);
                float lineEnergy = 0.0;
                float warnEnergy = 0.0;

                float digitScale = 0.018 * fontScale;
                lineEnergy += DrawDigitRun(uv, float2(0.085, 0.785), digitScale, _HectonVisorOxygenDigits) * 0.9;
                lineEnergy += DrawDigitRun(uv, float2(0.085, 0.705), digitScale, _HectonVisorDepthDigits) * 0.75;
                lineEnergy += DrawDigitRun(uv, float2(0.805, 0.785), digitScale, _HectonVisorPressureDigits) * 0.7;
                warnEnergy += DrawDigitRun(uv, float2(0.805, 0.705), digitScale, _HectonVisorWarningDigits) * stress;

                float frameLine = 0.0;
                frameLine += LineBox(uv, float2(0.5, 0.83), float2(0.36, 0.0018), 0.004);
                frameLine += LineBox(uv, float2(0.5, 0.17), float2(0.34, 0.0018), 0.004);
                frameLine += LineBox(uv, float2(0.12, 0.5), float2(0.0018, 0.22), 0.004);
                frameLine += LineBox(uv, float2(0.88, 0.5), float2(0.0018, 0.22), 0.004);
                lineEnergy += frameLine * lerp(0.22, 0.55, quality);

                int targetCount = min(HECTON_VISOR_MAX_TARGETS, max(0, (int)floor(_HectonVisorQualityAndTime.z + 0.5)));
                [loop]
                for (int i = 0; i < targetCount; i++)
                {
                    VisorArTargetDTO target = _HectonVisorArTargets[i];
                    float active = saturate(target.ScreenAndFlags.w);
                    float2 center = saturate(target.ScreenAndFlags.xy);
                    float scale = max(0.018, 0.052 * target.ShapeParams.z);
                    float bracket = DrawBracket(uv, center, scale, target.ShapeParams.x) * active;
                    float occluded = saturate(target.ShapeParams.y);
                    float pulse = 0.72 + 0.28 * sin(time * lerp(2.0, 7.0, quality) + target.ShapeParams.w * 1.37);
                    lineEnergy += bracket * pulse * (1.0 - occluded * 0.55);
                    warnEnergy += bracket * occluded * 0.6;
                    hudColor = lerp(hudColor, saturate(target.ColorAndPulse.rgb), bracket * active * 0.08);
                }

                float edge = saturate(radial);
                float scan = DrawScanline(uv, time, quality);
                float noise = ValueNoise(uv * lerp(48.0, 160.0, quality) + time * 0.11);
                float fog = smoothstep(0.24, 1.0, edge) * fogIntensity * (0.35 + 0.65 * noise);
                float lineAlpha = saturate(lineEnergy * (0.78 + scan * 0.22));
                float warnAlpha = saturate(warnEnergy * (0.9 + stress * 0.6));
                float3 arColor = hudColor * lineAlpha + warnColor * warnAlpha;
                float3 fogColor = lerp(float3(0.65, 0.82, 0.86), float3(1.0, 0.96, 0.9), stress);
                float3 result = source;
                result = lerp(result, result + arColor, saturate(lineAlpha + warnAlpha));
                result = lerp(result, fogColor, saturate(fog * lerp(0.18, 0.55, quality)));
                result += hudColor * scan * quality * 0.018;
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Hecton8/InternalBlackError"
}
