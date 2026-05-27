Shader "Hidden/Hecton8/Hecton_PdaScreen"
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
            Name "ScreenSpacePdaProjection"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            struct PdaStateDTO
            {
                float4x4 LocalToWorld;
                uint ActiveTabHashID;
                float BootSequenceProgress01;
                uint PdaFlags;
                uint _pad0;
            };

            StructuredBuffer<PdaStateDTO> _HectonPdaStateBuffer;

            CBUFFER_START(HectonPdaProjectionGlobals)
                float4 _HectonPdaScreenParams;
                float4 _HectonPdaRefractionParams;
                float4 _HectonPdaAtlasRect;
                float4 _HectonPdaVisualParams;
            CBUFFER_END

            #define H8_PDA_FLAG_ACTIVE 1u

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D(_HectonPdaInterfaceAtlas);
            SAMPLER(sampler_HectonPdaInterfaceAtlas);
            float4 _BlitTexture_TexelSize;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
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

            float Safe01(float value)
            {
                return saturate(value);
            }

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lenSq = dot(value, value);
                float valid = step(0.000001, lenSq);
                float3 normalized = value * rsqrt(max(lenSq, 0.000001));
                return lerp(fallback, normalized, valid);
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

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 1e-5));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float2 ResolveAtlasUv(float2 planeUv, float2 refractionOffset)
            {
                float2 warped = saturate(planeUv + refractionOffset);
                return _HectonPdaAtlasRect.xy + warped * _HectonPdaAtlasRect.zw;
            }

            float2 ClampAtlasUvToActiveRect(float2 atlasUv)
            {
                float2 rectMin = _HectonPdaAtlasRect.xy;
                float2 rectSize = max(abs(_HectonPdaAtlasRect.zw), float2(0.000002, 0.000002));
                float2 rectMax = rectMin + rectSize;
                float2 inset = max(rectSize * 0.001, float2(0.000001, 0.000001));
                return clamp(atlasUv, rectMin + inset, rectMax - inset);
            }

            float3 ResolveViewSpaceRay(float2 screenUV)
            {
                float4 farCS = float4(screenUV * 2.0 - 1.0, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                farCS.y = -farCS.y;
            #endif
                float4 farVS = mul(UNITY_MATRIX_I_P, farCS);
                float invW = rcp(max(abs(farVS.w), 0.000001));
                return SafeNormalize3(farVS.xyz * invW, float3(0.0, 0.0, -1.0));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
                float4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, ResolveFoveatedSourceUV(screenUV));
                PdaStateDTO state = _HectonPdaStateBuffer[0];

                float active = step(0.5, (float)(state.PdaFlags & H8_PDA_FLAG_ACTIVE));
                float boot = Safe01(state.BootSequenceProgress01) * Safe01(_HectonPdaRefractionParams.w);
                float quality = Safe01(_HectonPdaRefractionParams.x);
                float glassIor = max(1.0, _HectonPdaRefractionParams.y);
                float curvature = Safe01(_HectonPdaRefractionParams.z);
                float corruption = Safe01(_HectonPdaVisualParams.z);

                float3 rayOrigin = float3(0.0, 0.0, 0.0);
                float3 rayDir = ResolveViewSpaceRay(screenUV);
                float3x3 viewRotation = (float3x3)UNITY_MATRIX_V;
                float3 centerCameraRelativeWS = mul(state.LocalToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
                float3 rightCameraRelativeWS = mul((float3x3)state.LocalToWorld, float3(1.0, 0.0, 0.0));
                float3 upCameraRelativeWS = mul((float3x3)state.LocalToWorld, float3(0.0, 1.0, 0.0));
                float3 normalCameraRelativeWS = mul((float3x3)state.LocalToWorld, float3(0.0, 0.0, 1.0));
                float3 center = mul(viewRotation, centerCameraRelativeWS);
                float3 right = SafeNormalize3(mul(viewRotation, rightCameraRelativeWS), float3(1.0, 0.0, 0.0));
                float3 up = SafeNormalize3(mul(viewRotation, upCameraRelativeWS), float3(0.0, 1.0, 0.0));
                float3 normal = SafeNormalize3(mul(viewRotation, normalCameraRelativeWS), float3(0.0, 0.0, 1.0));
                normal *= lerp(-1.0, 1.0, step(0.0, dot(-rayDir, normal)));

                float denom = dot(rayDir, normal);
                float safeDenom = lerp(-0.0001, 0.0001, step(0.0, denom));
                denom = lerp(safeDenom, denom, step(0.0001, abs(denom)));
                float t = dot(center - rayOrigin, normal) / denom;
                float3 hit = rayOrigin + rayDir * t;
                float3 localDelta = hit - center;
                float2 planeUv = float2(
                    dot(localDelta, right) * _HectonPdaScreenParams.z + 0.5,
                    dot(localDelta, up) * _HectonPdaScreenParams.w + 0.5);
                float sceneRawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
                float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float planeEyeDepth = max(0.0, -hit.z);
                float depthMargin = lerp(0.004, 0.018, quality);
                float depthVisibility = ResolveLinearRamp01(-depthMargin, depthMargin, sceneEyeDepth - planeEyeDepth);

                float inside =
                    step(0.0, t) *
                    step(abs(planeUv.x - 0.5), 0.5) *
                    step(abs(planeUv.y - 0.5), 0.5) *
                    depthVisibility *
                    active *
                    boot;

                float4 directSample = SAMPLE_TEXTURE2D(_HectonPdaInterfaceAtlas, sampler_HectonPdaInterfaceAtlas, ResolveAtlasUv(planeUv, float2(0.0, 0.0)));
                float4 pdaColor = directSample;
                float refractionTier = ResolveLinearRamp01(0.20, 0.36, quality);
                if (refractionTier > 0.001)
                {
                    float incidence = 1.0 - saturate(abs(dot(-rayDir, normal)));
                    float curveMask = dot(planeUv - 0.5, planeUv - 0.5);
                    float refractionMagnitude = (glassIor - 1.0) * 0.018 * incidence * (0.25 + quality * 0.75);
                    float2 tangentRay = float2(dot(rayDir, right), dot(rayDir, up));
                    float2 curvatureOffset = (planeUv - 0.5) * curveMask * curvature * 0.035 * quality;
                    float2 refractionOffset = (tangentRay * refractionMagnitude + curvatureOffset) * quality * refractionTier;
                    float noise = Hash21(planeUv * _ScreenParams.xy + _HectonPdaVisualParams.y);
                    refractionOffset += (noise - 0.5) * corruption * 0.0025 * refractionTier;

                    float2 atlasUv = ResolveAtlasUv(planeUv, refractionOffset);
                    float4 refractedSample = SAMPLE_TEXTURE2D(_HectonPdaInterfaceAtlas, sampler_HectonPdaInterfaceAtlas, atlasUv);
                    float chromaTier = ResolveLinearRamp01(0.52, 0.88, quality);
                    if (chromaTier > 0.001)
                    {
                        float chroma = quality * quality * 0.65 * chromaTier;
                        float2 chromaOffset = refractionOffset * 0.65 * chroma;
                        float red = SAMPLE_TEXTURE2D(_HectonPdaInterfaceAtlas, sampler_HectonPdaInterfaceAtlas, ClampAtlasUvToActiveRect(atlasUv + chromaOffset)).r;
                        float blue = SAMPLE_TEXTURE2D(_HectonPdaInterfaceAtlas, sampler_HectonPdaInterfaceAtlas, ClampAtlasUvToActiveRect(atlasUv - chromaOffset)).b;
                        refractedSample.rgb = lerp(refractedSample.rgb, float3(red, refractedSample.g, blue), chroma);
                    }

                    pdaColor = lerp(directSample, refractedSample, refractionTier);
                }

                float edgeFade = ResolveLinearRamp01(0.0, 0.055, min(min(planeUv.x, 1.0 - planeUv.x), min(planeUv.y, 1.0 - planeUv.y)));
                float alpha = saturate(pdaColor.a * inside * edgeFade * (0.85 + quality * 0.35));
                float3 emissive = pdaColor.rgb * (0.85 + _HectonPdaVisualParams.x * 0.15) + float3(0.02, 0.08, 0.07) * alpha;
                return float4(lerp(sourceColor.rgb, emissive, alpha), sourceColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
