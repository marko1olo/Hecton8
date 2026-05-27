Shader "Hecton8/UI/DiegeticVisorCurvedHUD"
{
    Properties
    {
        _BaseMap ("HUD Texture", 2D) = "black" {}
        _DirtTex ("Lens Dirt", 2D) = "black" {}
        _Tint ("HUD Tint", Color) = (0.72, 1.0, 0.88, 0.94)
        _EmissionGain ("Emission Gain", Range(0, 8)) = 2.1
        _AlphaGain ("Alpha Gain", Range(0, 8)) = 3.0
        _BlackCutoff ("Black Cutoff", Range(0, 0.25)) = 0.015
        _EdgeFade ("Edge Fade", Range(0, 0.5)) = 0.08
        _ChromaticStrength ("Chromatic Strength", Range(0, 3)) = 0.65
        _DamageGlitch ("Damage Glitch", Range(0, 1)) = 0
        _Humidity01 ("Humidity", Range(0, 1)) = 0
        _DirtStrength ("Dirt Strength", Range(0, 2)) = 0.55
        _PanelPowerLevel ("Panel Power", Range(0, 1)) = 1
        _StencilRef ("Visor Stencil Ref", Float) = 17
        _DitherCoverageBias ("Dither Coverage Bias", Range(-0.25, 0.25)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+90"
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
            Name "DiegeticVisorCurvedHUD"
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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DirtTex_ST;
                float4 _Tint;
                float _EmissionGain;
                float _AlphaGain;
                float _BlackCutoff;
                float _EdgeFade;
                float _ChromaticStrength;
                float _DamageGlitch;
                float _Humidity01;
                float _DirtStrength;
                float _PanelPowerLevel;
                float _StencilRef;
                float _DitherCoverageBias;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DirtTex);
            SAMPLER(sampler_DirtTex);

            float _HectonVRBrownoutIntensity;

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 1e-5));
            }

            float SignedTriangleWave(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float tearBand = step(0.82, frac(input.uv.y * 18.0 + _Time.y * 5.0));
                float tearWave = SignedTriangleWave((input.uv.y * 96.0) + (_Time.y * 70.0));
                positionOS.x += tearWave * tearBand * _DamageGlitch * 0.018;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;
                float2 absCenter = abs(centered) * 2.0;
                float edge = max(absCenter.x, absCenter.y);
                float edgeFade = 1.0 - ResolveLinearRamp01(1.0 - _EdgeFade, 1.0, edge);

                float chromaWeight = saturate(dot(centered, centered) * 4.5);
                float4 centerSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float fakeChroma = chromaWeight * saturate(_ChromaticStrength) * 0.18;
                float3 hudRgb = centerSample.rgb * float3(1.0 + fakeChroma, 1.0, 1.0 - fakeChroma * 0.55);
                hudRgb += float3(fakeChroma * 0.025, 0.0, fakeChroma * 0.012);

                float maxRgb = max(max(hudRgb.r, hudRgb.g), hudRgb.b);
                float rgbAlpha = saturate((maxRgb - _BlackCutoff) * _AlphaGain);
                float alpha = max(centerSample.a, rgbAlpha) * _Tint.a * edgeFade;

                float brownout = saturate(_HectonVRBrownoutIntensity);
                float flicker = abs(frac((_Time.y * 13.0) + (input.uv.y * 6.0)) * 2.0 - 1.0);
                float power = saturate(_PanelPowerLevel) * (1.0 - (brownout * lerp(0.22, 0.62, flicker)));

                float dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, TRANSFORM_TEX(input.uv, _DirtTex)).r;
                float dirtMask = saturate(dirt * _Humidity01 * _DirtStrength);
                float3 color = hudRgb * _Tint.rgb * _EmissionGain * lerp(0.24, 1.0, power);
                color = lerp(color, (color * 0.42) + (_Tint.rgb * 0.08), dirtMask);
                alpha = max(alpha, dirtMask * 0.22) * power;

                ClipDitheredCoverage(alpha, input.positionCS.xy);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Hecton8/InternalBlackError"
}
