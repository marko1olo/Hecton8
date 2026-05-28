Shader "Hecton8/UI/HUDDiegeticProjectionUnlit"
{
    Properties
    {
        _BaseMap ("HUD Render Texture", 2D) = "black" {}
        _MainTex ("HUD Main Texture", 2D) = "black" {}
        _Color ("HUD Tint", Color) = (0.78, 1.0, 0.94, 0.96)
        _Intensity ("HUD Intensity", Range(0, 8)) = 2.2
        _AlphaGain ("RGB Alpha Gain", Range(0, 8)) = 3.0
        _BlackCutoff ("Black Cutoff", Range(0, 0.25)) = 0.015
        _EdgeFade ("Panel Edge Fade", Range(0, 0.5)) = 0.06
        _FrameAlpha ("Physical Frame Alpha", Range(0, 0.5)) = 0.12
        _PanelPowerLevel ("Panel Power", Range(0, 1)) = 1
        _StencilRef ("Visor Stencil Ref", Float) = 1
        _DitherCoverageBias ("Dither Coverage Bias", Range(-0.25, 0.25)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+80"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "HUDDiegeticProjection"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilRef]
                Comp Equal
                Pass Keep
                ReadMask 255
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _Intensity;
                float _AlphaGain;
                float _BlackCutoff;
                float _EdgeFade;
                float _FrameAlpha;
                float _PanelPowerLevel;
                float _StencilRef;
                float _DitherCoverageBias;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;
            float _H8GlobalQualityWeight;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float Bayer4x4(float2 pixelPosition)
            {
                float2 cell = floor(frac(pixelPosition * 0.25) * 4.0);

                if (cell.y < 0.5)
                {
                    if (cell.x < 0.5) return 0.0 / 16.0;
                    if (cell.x < 1.5) return 8.0 / 16.0;
                    if (cell.x < 2.5) return 2.0 / 16.0;
                    return 10.0 / 16.0;
                }

                if (cell.y < 1.5)
                {
                    if (cell.x < 0.5) return 12.0 / 16.0;
                    if (cell.x < 1.5) return 4.0 / 16.0;
                    if (cell.x < 2.5) return 14.0 / 16.0;
                    return 6.0 / 16.0;
                }

                if (cell.y < 2.5)
                {
                    if (cell.x < 0.5) return 3.0 / 16.0;
                    if (cell.x < 1.5) return 11.0 / 16.0;
                    if (cell.x < 2.5) return 1.0 / 16.0;
                    return 9.0 / 16.0;
                }

                if (cell.x < 0.5) return 15.0 / 16.0;
                if (cell.x < 1.5) return 7.0 / 16.0;
                if (cell.x < 2.5) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            void ClipDitheredCoverage(float coverage, float2 positionCS)
            {
                float threshold = Bayer4x4(positionCS);
                clip(saturate(coverage + _DitherCoverageBias) - threshold);
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centerUv = abs(input.uv - 0.5) * 2.0;
                float edge = max(centerUv.x, centerUv.y);
                float edgeFade = 1.0 - saturate((edge - (1.0 - _EdgeFade)) * rcp(max(_EdgeFade, 0.0001)));
                float frameMask = step(0.982, edge);
                float powerLevel = saturate(_PanelPowerLevel);
                if (powerLevel < 0.1)
                {
                    float dither = Bayer4x4(floor(input.positionCS.xy));
                    float phosphorBit = step(dither, 0.375);
                    float ditherAlpha = saturate((phosphorBit * 0.58 + frameMask * 0.42) * edgeFade * _Color.a);
                    ClipDitheredCoverage(ditherAlpha, input.positionCS.xy);
                    float2 fallbackComfortUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                    float fallbackComfortBlack = ResolveHectonComfortBlackAmount(fallbackComfortUV, input.positionCS.xy);
                    half3 fallbackColor = lerp(half3(0.02h, 0.92h, 0.24h), half3(0.0015h, 0.0023h, 0.0031h), (half)fallbackComfortBlack);
                    return half4(fallbackColor, 1.0h);
                }

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 hudSample = max(baseSample, mainSample);

                float maxRgb = max(max(hudSample.r, hudSample.g), hudSample.b);
                float rgbAlpha = saturate((maxRgb - _BlackCutoff) * _AlphaGain);
                float alpha = max(hudSample.a, rgbAlpha) * _Color.a * powerLevel * edgeFade;
                float3 color = hudSample.rgb * _Color.rgb * _Intensity * lerp(0.45, 1.0, powerLevel);
                color += _Color.rgb * frameMask * _FrameAlpha;
                alpha = max(alpha, frameMask * _FrameAlpha);

                ClipDitheredCoverage(alpha, input.positionCS.xy);
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                color = lerp(color, float3(0.0015, 0.0023, 0.0031), comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Hecton8/InternalBlackError"
}
