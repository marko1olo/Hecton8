Shader "Hecton8/UI/DiegeticPanelUnlit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "black" {}
        _MainTex ("Main Tex", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _PanelPowerLevel ("Panel Power", Range(0, 1)) = 1
        _DepthFadeRange ("Depth Fade Range", Range(0.001, 1)) = 0.08
        _OcclusionActive ("Occlusion Active", Float) = 0
        _InventoryScanlineStrength ("Inventory Scanline Strength", Range(0, 1)) = 0.22
        _InventoryScanlineDensity ("Inventory Scanline Density", Range(16, 320)) = 140
        _CrtCurvature ("CRT Curvature", Range(0, 0.08)) = 0.018
        _CrtScanlineStrength ("CRT Scanline Strength", Range(0, 1)) = 0.16
        _FlashlightGlare ("Flashlight Glare", Range(0, 1)) = 0
        _TerminalDamageGlitch ("Terminal Damage Glitch", Range(0, 1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _StencilRef ("Stencil Reference", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite On
        ZTest LEqual
        Blend Off
        AlphaToMask On
        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            Comp [_StencilComp]
            Pass Keep
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BaseMap_ST;
                float _PanelPowerLevel;
                float _DepthFadeRange;
                float _OcclusionActive;
                float _InventoryScanlineStrength;
                float _InventoryScanlineDensity;
                float _CrtCurvature;
                float _CrtScanlineStrength;
                float _FlashlightGlare;
                float _TerminalDamageGlitch;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _HectonPdaInventoryParallax;
            float4 _HectonUiAnalogJitter;
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonDiegeticGlitchIntensity;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;


            float _H8GlobalQualityWeight;
            float Bayer4x4(float2 pixelCoord)
            {
                float2 cell = floor(frac(pixelCoord * 0.25) * 4.0);

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

            float Hash21(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 1e-5));
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float powerLevel = saturate(_PanelPowerLevel);
                if (powerLevel < 0.05)
                {
                    float bayer = Bayer4x4(floor(input.positionCS.xy));
                    float2 edgeDistanceBrownout = min(input.uv, 1.0 - input.uv);
                    float edgeMaskBrownout = 1.0 - ResolveLinearRamp01(0.012, 0.075, min(edgeDistanceBrownout.x, edgeDistanceBrownout.y));
                    float phosphorBit = step(bayer, 0.375);
                    float ditherAlpha = saturate((0.18 + phosphorBit * 0.44 + edgeMaskBrownout * 0.16) * _Color.a);
                    clip(ditherAlpha - bayer);
                    float2 fallbackComfortUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                    float fallbackComfortBlack = ResolveHectonComfortBlackAmount(fallbackComfortUV, input.positionCS.xy);
                    half3 fallbackColor = lerp(half3(0.018h, 0.84h, 0.20h), half3(0.0015h, 0.0023h, 0.0031h), (half)fallbackComfortBlack);
                    return half4(fallbackColor, 1.0h);
                }

                float inventoryMask = saturate(_HectonPdaInventoryParallax.z);
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float2 crtUv = input.uv + centeredUv * dot(centeredUv, centeredUv) * saturate(_CrtCurvature);
                float crtBounds = step(0.0, crtUv.x) * step(crtUv.x, 1.0) * step(0.0, crtUv.y) * step(crtUv.y, 1.0);
                float2 panelSampleUv = saturate(crtUv);
                float2 panelUv = panelSampleUv + (panelSampleUv - 0.5) * _HectonPdaInventoryParallax.xy;
                float screenCenteredX = (input.positionCS.x * rcp(max(1.0, _ScaledScreenParams.x))) - 0.5;
                panelUv.x += screenCenteredX * _HectonPdaInventoryParallax.x * inventoryMask * 0.08;

                float damageGlitch = saturate(max(_TerminalDamageGlitch, _HectonDiegeticGlitchIntensity));
                float glitchCell = floor(panelSampleUv.y * 48.0);
                float glitchNoise = Hash21(float2(glitchCell, floor(_Time.y * 24.0)));
                float glitchWave = (FastTrianglePulse01(_Time.y * 31.0 + glitchCell * 0.73 + glitchNoise * 6.28318) * 2.0 - 1.0);
                float glitchGate = step(0.58, glitchNoise) * damageGlitch;
                panelUv.x += glitchWave * glitchGate * 0.014;
                panelUv.x += step(frac(panelSampleUv.y * 100.0 + _Time.y * 7.0), damageGlitch) * damageGlitch * 0.1;

                float analogStrength = saturate(_HectonUiAnalogJitter.x) * inventoryMask;
                float2 analogCell = floor(panelSampleUv * float2(113.0, 47.0));
                float analogNoise = Hash21(analogCell);
                float analogWave = FastTrianglePulse01(_Time.y * 100.0 + panelSampleUv.y * 613.0 + analogNoise * 6.28318) * 2.0 - 1.0;
                panelUv += float2(
                    analogWave * (analogNoise - 0.5) * analogStrength * 0.006,
                    analogWave * analogStrength * 0.0015);
                panelUv = saturate(panelUv);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, panelUv);
                half4 screenSample = baseSample;
                float rgbAlpha = saturate(max(max(screenSample.r, screenSample.g), screenSample.b) * 2.0);
                float3 emissive = screenSample.rgb * _Color.rgb * lerp(0.45, 1.0, powerLevel);
                float alpha = max(screenSample.a, rgbAlpha) * _Color.a * crtBounds;
                float scanCoord = frac(panelSampleUv.y * max(1.0, _InventoryScanlineDensity) + _Time.y * 0.85);
                float scanline = lerp(1.0, lerp(0.72, 1.08, step(0.5, scanCoord)), inventoryMask * _InventoryScanlineStrength);
                float crtScanCoord = frac(panelSampleUv.y * 180.0 + _Time.y * 0.35);
                float crtScanline = lerp(1.0, 0.78 + 0.22 * step(0.5, crtScanCoord), saturate(_CrtScanlineStrength));
                emissive *= scanline * crtScanline * crtBounds;
                alpha *= lerp(1.0, 0.92 + scanline * 0.08, inventoryMask);
                float damageSpark = step(0.86, Hash21(floor(panelSampleUv * float2(96.0, 32.0)) + floor(_Time.y * 18.0))) * damageGlitch;
                emissive += float3(0.06, 0.86, 0.28) * damageSpark * 0.32;
                alpha *= lerp(1.0, 0.82 + 0.18 * step(0.5, glitchWave), damageGlitch);
                emissive *= lerp(1.0, 0.5, saturate(_FlashlightGlare));
                alpha *= lerp(1.0, 0.72, saturate(_FlashlightGlare));
                float2 edgeDistance = min(panelSampleUv, 1.0 - panelSampleUv);
                float edgePulseMask = (1.0 - ResolveLinearRamp01(0.012, 0.075, min(edgeDistance.x, edgeDistance.y))) * inventoryMask * powerLevel;
                float edgePulse = 0.74 + 0.26 * (FastTrianglePulse01(_Time.y * 4.7 + panelSampleUv.x * 13.0 - panelSampleUv.y * 9.0) * 2.0 - 1.0);
                emissive += _Color.rgb * edgePulseMask * edgePulse * 0.085;
                alpha = saturate(alpha + edgePulseMask * edgePulse * 0.035);

                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                emissive = lerp(emissive, float3(0.0015, 0.0023, 0.0031), comfortBlackAmount);
                alpha = saturate(max(alpha, comfortBlackAmount));

                if (_OcclusionActive > 0.5 && alpha > 0.001)
                {
                    float2 screenUV = input.positionCS.xy * rcp(_ScaledScreenParams.xy);
                    float fragRawDepth = saturate(input.positionCS.z * rcp(input.positionCS.w));
                    float sceneRawDepth = SampleSceneDepth(ResolveFoveatedSourceUV(screenUV));
#if UNITY_REVERSED_Z
                    float sceneDepthValid = step(0.0001, sceneRawDepth);
#else
                    float sceneDepthValid = step(sceneRawDepth, 0.9999);
#endif
                    float linearSceneDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                    float linearFragDepth = LinearEyeDepth(fragRawDepth, _ZBufferParams);
                    float occluded = sceneDepthValid * step(linearSceneDepth + _DepthFadeRange, linearFragDepth);

                    if (occluded > 0.5)
                    {
                        float2 screenPixel = floor(input.positionCS.xy);
                        float bayer = Bayer4x4(screenPixel);
                        float ditherGate = step(bayer, 0.3125);
                        float weakProjection = lerp(0.08, 0.46, ditherGate);
                        emissive *= weakProjection;
                        alpha *= lerp(0.035, 0.18, ditherGate);
                    }
                }

                clip(alpha - Bayer4x4(floor(input.positionCS.xy)));
                return half4(emissive, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
