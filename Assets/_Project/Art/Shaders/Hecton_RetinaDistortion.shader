Shader "Hidden/Hecton8/RetinaDistortion"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "RetinaDistortion"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            CBUFFER_START(HectonRetinaDistortionGlobals)
                float4 _HectonRetinaParams0;
                float4 _HectonRetinaParams1;
            CBUFFER_END

            #define _HectonRetinaHealth01 _HectonRetinaParams0.x
            #define _HectonRetinaCritical01 _HectonRetinaParams0.y
            #define _HectonRetinaHeartbeatBpm _HectonRetinaParams0.z
            #define _HectonNarcosisScalar _HectonRetinaParams0.w
            #define _HectonRetinaChromaticOffset _HectonRetinaParams1.x
            #define _HectonRetinaDistortionOffset _HectonRetinaParams1.y
            #define _HectonRetinaVignetteStrength _HectonRetinaParams1.z
            #define _HectonRetinaQualityWeight _HectonRetinaParams1.w

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

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
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

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(uv);
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

            float HeartbeatLobe(float phase01, float center01, float width01)
            {
                float lobe = saturate(1.0 - abs(phase01 - center01) / max(width01, 0.0001));
                return lobe * lobe * (3.0 - 2.0 * lobe);
            }

            float HectonRetinaSmoothRange01(float low, float high, float value)
            {
                float t = saturate((value - low) / max(high - low, 0.0001));
                return t * t * (3.0 - 2.0 * t);
            }

            float HeartbeatPulse(float bpm)
            {
                float phase01 = frac(_Time.y * max(1.0, bpm) * (1.0 / 60.0));
                float primary = HeartbeatLobe(phase01, 0.045, 0.070);
                float secondary = HeartbeatLobe(phase01, 0.205, 0.052) * 0.62;
                return saturate(primary + secondary);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float critical01 = saturate(_HectonRetinaCritical01);
                float narcosis01 = saturate(_HectonNarcosisScalar);
                [branch]
                if (max(critical01, narcosis01) <= 0.0001)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(input.screenUV));

                float2 centered = input.screenUV * 2.0 - 1.0;
                float distSq = dot(centered, centered);
                float edge01 = saturate(distSq * 1.35);
                float2 radialDir = centered * rsqrt(max(distSq, 0.00001));

                float pulse01 = HeartbeatPulse(_HectonRetinaHeartbeatBpm);
                float noise = ValueNoise(input.screenUV * float2(9.0, 7.0) + _Time.y * 0.31) - 0.5;
                float pulseDrive = critical01 * (0.62 + pulse01 * 0.58);
                float narcosisDrive = narcosis01 * edge01 * (0.64 + abs(noise) * 0.36);
                float distortion = _HectonRetinaDistortionOffset * edge01 * max(pulseDrive, narcosisDrive * 0.78) * (1.0 + noise * 0.34);
                float chroma = _HectonRetinaChromaticOffset * edge01 * max(pulseDrive, narcosisDrive);
                float retinaQuality = HectonRetinaSmoothRange01(0.0, 1.0, _HectonRetinaQualityWeight);
                distortion *= retinaQuality;
                chroma *= HectonRetinaSmoothRange01(0.45, 0.95, retinaQuality);
                float2 refractedUV = saturate(input.screenUV + radialDir * distortion);
                float2 chromaOffset = radialDir * chroma;

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(refractedUV));
                [branch]
                if (abs(chroma) > 0.000001)
                {
                    half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(refractedUV + chromaOffset))).r;
                    half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(saturate(refractedUV - chromaOffset))).b;
                    color.r = red;
                    color.b = blue;
                }

                half luminance = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half desaturate01 = (half)(max(critical01 * 0.12, narcosis01 * 0.18) * edge01);
                color.rgb = lerp(color.rgb, luminance.xxx * half3(0.82h, 0.94h, 1.08h), desaturate01);

                half vignette = (half)saturate(_HectonRetinaVignetteStrength * edge01 * max(0.48 + pulse01 * 0.52, 0.62 + narcosis01 * 0.28));
                color.rgb *= 1.0h - vignette;
                color.rgb = max(color.rgb, half3(0.0015h, 0.0023h, 0.0031h));
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
