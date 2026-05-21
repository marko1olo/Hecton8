Shader "Hidden/Hecton8/VisorGlitchACES"
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
            Name "DeepSeaNoirPost"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(NoirPostProcessDTO)
                float4 GrainParams;
                float4 AberrationParams;
                float4 ColorGrading;
                float4 QualityAndLimits;
            CBUFFER_END

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

            float Hash21(float2 p)
            {
                p = all(isfinite(p)) ? p : float2(0.0, 0.0);
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float TriangleWave(float value)
            {
                return abs(frac(value) * 2.0 - 1.0);
            }

            float Smooth01(float value)
            {
                value = saturate(value);
                return value * value * (3.0 - 2.0 * value);
            }

            float ResolveTornVisorEdgeMask(float2 uv, float edge01, float damage01, float stress01, float timeWrapped)
            {
                float edgeBand = Smooth01((edge01 - 0.18) * 1.219512);
                float2 serrationUv = uv * float2(31.0, 19.0) + timeWrapped * float2(0.021, 0.013);
                float coarse = Hash21(floor(serrationUv));
                float fine = Hash21(floor(serrationUv * 2.73 + coarse));
                float tearNoise = lerp(coarse, fine, Smooth01(damage01));
                float threshold = lerp(0.93, 0.43, saturate(damage01 * 0.72 + stress01 * 0.36));
                return edgeBand * step(threshold, tearNoise);
            }

            float ResolveProceduralCrackMask(float2 uv, float damage01, float quality, float timeWrapped, out float2 crackNormal)
            {
                float2 shardUv = uv * lerp(18.0, 62.0, Smooth01(quality)) + timeWrapped * 0.017;
                float2 cell = floor(shardUv);
                float2 fracUv = frac(shardUv) - 0.5;
                float n0 = Hash21(cell);
                float n1 = Hash21(cell + 13.71);
                float2 axis = float2(n0 - 0.5, n1 - 0.5);
                float axisLenSq = max(dot(axis, axis), 0.0001);
                axis *= rsqrt(axisLenSq);
                float veinWidth = max(0.012, lerp(0.036, 0.012, quality));
                float vein = 1.0 - smoothstep(0.004, veinWidth, abs(dot(fracUv, axis)));
                float reveal = step(lerp(0.96, 0.28, damage01), n0) * vein;
                crackNormal = axis * reveal;
                return reveal;
            }

            float2 DearLieOffset(float2 uv, float quality, float stress, float toxicity, float timeWrapped)
            {
                float highMath = Smooth01((quality - 0.62) * 2.631579);
                float ultraMath = Smooth01((quality - 0.84) * 6.25);
                float glitchX = Finite01(AberrationParams.y);
                float glitchY = Finite01(AberrationParams.z);
                float blockScale = lerp(18.0, 90.0, highMath);
                float2 blockUv = floor(uv * blockScale) / blockScale;
                float blockNoise = Hash21(blockUv + float2(timeWrapped * 0.013, toxicity * 7.1));
                float snap = step(0.74 - toxicity * 0.24 - stress * 0.18, blockNoise);
                float signedNoise = blockNoise * 2.0 - 1.0;
                float amplitude = (0.0008 + 0.0038 * quality) * (0.35 + stress + toxicity);
                float cheap = signedNoise * snap;
                float stochasticBudget = saturate(highMath + toxicity * 0.18 + stress * 0.12);
                float detailMask = step(1.0 - stochasticBudget, blockNoise);
                float stripe = TriangleWave(uv.y * lerp(28.0, 116.0, highMath) + timeWrapped * lerp(0.22, 1.4, quality)) * detailMask;
                float wave = sin((uv.y + signedNoise * 0.07) * lerp(38.0, 144.0, ultraMath) + timeWrapped * 2.1) * detailMask;
                float overkill = cheap + wave * stripe * ultraMath;
                return float2(lerp(cheap, overkill, highMath) * glitchX, wave * 0.12 * ultraMath * glitchY) * amplitude;
            }

            float Grain(float2 uv, float quality, float stress, float toxicity, float timeWrapped)
            {
                float intensity = max(0.0, GrainParams.x);
                float scale = max(8.0, GrainParams.y);
                float speed = max(0.0, GrainParams.z);
                float highMath = Smooth01((quality - 0.42) * 1.724138);
                float ultraMath = Smooth01((quality - 0.78) * 4.545454);
                float2 pixelUv = uv * scale;
                float cheap = Hash21(floor(pixelUv) + timeWrapped * speed);
                float folded = frac(cheap * lerp(17.0, 83.0, highMath) + toxicity * 0.37 + timeWrapped * 0.007);
                float detail = lerp(cheap, (cheap + folded) * 0.5, highMath);
                float sparkleMask = step(0.985 - ultraMath * 0.14 - stress * 0.025, folded);
                float sparkle = (folded - 0.5) * ultraMath * stress * sparkleMask;
                return (detail - 0.5 + sparkle) * intensity;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ResolveXRStereoScreenUV(input.screenUV);
                uv = all(isfinite(uv)) ? saturate(uv) : float2(0.5, 0.5);

                float quality = Finite01(QualityAndLimits.x);
                float stress = Finite01(QualityAndLimits.y);
                float toxicity = Finite01(QualityAndLimits.z);
                float abSplit = Finite01(QualityAndLimits.w);
                float timeWrapped = isfinite(GrainParams.w) ? GrainParams.w : 0.0;
                float2 centered = uv * 2.0 - 1.0;
                float edge01 = saturate((dot(centered, centered) - 0.34) * 1.72);
                float woundDrive = saturate(stress * 0.72 + toxicity * 0.42);
                float tornEdgeMask = ResolveTornVisorEdgeMask(uv, edge01, woundDrive, stress, timeWrapped);
                float2 crackNormal;
                float crackMask = ResolveProceduralCrackMask(uv, woundDrive, quality, timeWrapped, crackNormal);
                float2 edgeNormal = centered * rsqrt(max(dot(centered, centered), 0.0001));
                float2 woundOffset = (edgeNormal * tornEdgeMask * 0.35 + crackNormal * crackMask) *
                                      lerp(0.00045, 0.0038, Smooth01(quality));

                float2 glitchOffset = DearLieOffset(uv, quality, stress, toxicity, timeWrapped);
                float2 sampleUv = saturate(uv + glitchOffset + woundOffset);
                float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUv);
                float3 color = Finite3(source.rgb, 0.0);
                float sourceAlpha = isfinite(source.a) ? source.a : 1.0;

                float chroma = max(0.0, AberrationParams.x) * (0.45 + stress + toxicity);
                float chromaGate = Smooth01((quality - 0.2) * 1.25) * saturate(stress + toxicity + chroma * 80.0);
                float chromaNoise = Hash21(uv * _ScreenParams.xy + timeWrapped * 0.07);
                float chromaMask = step(chromaNoise, chromaGate);
                float lumaPreGrade = dot(color, float3(0.2126, 0.7152, 0.0722));
                float chromaPhase = (chromaNoise - 0.5) *
                                    chromaMask *
                                    saturate(chroma * 96.0 + stress * 0.18 + toxicity * 0.22);
                color = max(color + float3(chromaPhase, 0.0, -chromaPhase) * (0.18 + lumaPreGrade * 0.12), 0.0);

                float contrast = max(0.25, ColorGrading.x);
                float saturation = saturate(ColorGrading.y);
                float temperature = clamp(ColorGrading.z, -1.0, 1.0);
                float depthTone = Finite01(ColorGrading.w);
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luma.xxx, color, saturation);
                color = (color - 0.5) * contrast + 0.5;
                color *= 1.0 + temperature * float3(0.08, 0.0, -0.08);
                color = lerp(color, color * float3(0.62, 0.79, 1.08), depthTone);
                color = max(color, 0.0);

                float vignette = saturate(dot(centered, centered));
                color *= 1.0 - vignette * Finite01(AberrationParams.w) * 0.62;
                color += Grain(uv, quality, stress, toxicity, timeWrapped);
                color *= 1.0 - crackMask * (0.16 + edge01 * 0.08);
                color *= 1.0 - tornEdgeMask * (0.08 + quality * 0.08);
                color += float3(0.11, 0.024, 0.018) * tornEdgeMask * (0.22 + stress * 0.58);
                color += float3(0.05, 0.065, 0.072) * crackMask * (0.035 + quality * 0.025);
                color = max(Finite3(color, 0.0), 0.0);

                float rawBlend = abSplit * step(uv.x, 0.5);
                color = Finite3(lerp(color, source.rgb, rawBlend), color);
                return float4(max(color, 0.0), sourceAlpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
