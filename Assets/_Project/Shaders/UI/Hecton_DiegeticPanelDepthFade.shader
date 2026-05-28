Shader "Hecton8/UI/Diegetic Panel Depth Fade"
{
    Properties
    {
        _BaseMap ("Panel Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _DepthFadeRange ("Depth Fade Range", Float) = 0.05
        _OcclusionActive ("Occlusion Active", Float) = 1
        _PanelPowerLevel ("Panel Power", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest+10"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;

            float _H8GlobalQualityWeight;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _DepthFadeRange;
                float _OcclusionActive;
                float _PanelPowerLevel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionVS : TEXCOORD2;
            };

            float ResolveBayerThreshold(float2 screenUV)
            {
                uint2 screenPixel = (uint2)floor(screenUV * _ScreenParams.xy);
                uint column = screenPixel.x & 3u;
                uint row = screenPixel.y & 3u;
                uint index = column | (row << 2);

                const float4 row0 = float4(0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0);
                const float4 row1 = float4(12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0);
                const float4 row2 = float4(3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0);
                const float4 row3 = float4(15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0);

                if (index < 4u)
                    return row0[index];
                if (index < 8u)
                    return row1[index - 4u];
                if (index < 12u)
                    return row2[index - 8u];
                return row3[index - 12u];
            }

            float2 ResolveFoveatedSourceUV(float2 uv)
            {
                return FoveatedRemapLinearToNonUniform(saturate(uv));
            }

            float HectonComfortIgn(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveHectonComfortEyeStableScreenUV(float2 positionCS)
            {
                float2 screenUV = saturate(positionCS * rcp(max(_ScreenParams.xy, float2(1.0, 1.0))));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                float4 stereoScaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                screenUV = (screenUV - stereoScaleOffset.zw) * rcp(max(stereoScaleOffset.xy, float2(0.0001, 0.0001)));
#endif
                return saturate(screenUV);
            }

            float ResolveHectonComfortBlackAmount(float2 screenUV, float2 positionCS)
            {
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float somaticTunnel = saturate(_HectonVRSomaticComfortState.x);
                float vrComfortTunnel = saturate(max(max(_HectonVrComfortSignals.x, _HectonVrComfortMotion.z) * vrComfortEnabled, max(_HectonTunnelingIntensity, somaticTunnel)));
                float vrComfortBlackout = saturate(max(_HectonVrComfortSignals.y * vrComfortEnabled, _HectonVRBrownoutIntensity));
                float2 radial = screenUV * 2.0 - 1.0;
                radial.x *= _ScreenParams.x * rcp(max(_ScreenParams.y, 1.0));
                float radialMagnitudeSq = saturate(dot(radial, radial));
                float tunnelInner = lerp(0.74, 0.34, vrComfortTunnel);
                float tunnelInnerSq = tunnelInner * tunnelInner;
                float tunnelMask = saturate((radialMagnitudeSq - tunnelInnerSq) * rcp(max(1.0 - tunnelInnerSq, 0.0009765625))) * vrComfortTunnel;
                float ign = HectonComfortIgn(floor(positionCS));
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.positionVS = TransformWorldToView(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                baseColor.a *= saturate(_PanelPowerLevel);

                if (_OcclusionActive > 0.5)
                {
                    float sceneDepthRaw = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
                    float linearSceneDepth = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                    float linearFragmentDepth = -input.positionVS.z;

                    if (isnan(linearSceneDepth) || isinf(linearSceneDepth))
                        linearSceneDepth = linearFragmentDepth;

                    float depthDelta = linearSceneDepth - linearFragmentDepth;
                    float fadeRange = max(_DepthFadeRange, 1e-4);
                    float occlusionFactor = saturate(depthDelta / fadeRange);
                    float ditherThreshold = ResolveBayerThreshold(screenUV);
                    clip(occlusionFactor - ditherThreshold);
                    baseColor.a *= occlusionFactor;
                }

                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                baseColor.rgb = lerp(baseColor.rgb, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                baseColor.a = max(baseColor.a, (half)comfortBlackAmount);

                clip(baseColor.a - max(ResolveBayerThreshold(screenUV), 0.001));
                return half4(baseColor.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
