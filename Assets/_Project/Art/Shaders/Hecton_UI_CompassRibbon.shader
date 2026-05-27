Shader "Hecton8/UI/CompassRibbon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Compass Ribbon", 2D) = "white" {}
        _Color ("Tint", Color) = (0.58, 0.96, 1.0, 0.72)
        _CompassOffset ("Compass Offset", Range(0, 1)) = 0
        _TickDensity ("Tick Density", Range(8, 128)) = 48
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.18
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "CompassRibbon"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _CompassOffset;
                float _TickDensity;
                float _ScanlineStrength;
                float _PulseStrength;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 0.000001));
            }

            float ResolveLinearRampInv01(float edge0, float edge1, float value)
            {
                return 1.0 - ResolveLinearRamp01(edge0, edge1, value);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float scrollX = frac(uv.x + _CompassOffset);

                float minorTick = ResolveLinearRampInv01(0.010, 0.026, abs(frac(scrollX * _TickDensity) - 0.5));
                float majorTick = ResolveLinearRampInv01(0.012, 0.040, abs(frac(scrollX * 4.0) - 0.5));
                float centerBand = ResolveLinearRamp01(0.18, 0.42, uv.y) * ResolveLinearRampInv01(0.58, 0.82, uv.y);
                float centerNotch = ResolveLinearRampInv01(0.015, 0.055, abs(uv.x - 0.5));
                float scanline = lerp(1.0, 0.82 + 0.18 * step(0.5, frac(uv.y * 96.0 + _Time.y)), _ScanlineStrength);
                float mask = saturate((minorTick * 0.24 + majorTick * 0.78 + centerNotch * 0.7) * centerBand);
                float pulseWindow = ResolveLinearRampInv01(0.0, 0.42, abs(uv.x - 0.5));
                float pulse = FastTrianglePulse01(_Time.y * 5.7 + scrollX * 37.0) * pulseWindow * _PulseStrength;
                float sweepCenter = frac(_Time.y * 0.17 + _CompassOffset);
                float sweepDelta = abs(frac((uv.x - sweepCenter) + 0.5) - 0.5);
                float sweepPulse = ResolveLinearRampInv01(0.0, 0.065, sweepDelta) * centerBand * _PulseStrength;
                if (max(mask, pulse + sweepPulse) <= 0.0001)
                {
                    clip(-1.0);
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(scrollX, uv.y));

                half3 color = max(source.rgb * source.a * (half)mask, _Color.rgb * (half)mask) * (half)scanline;
                color = saturate(color + _Color.rgb * (half)(pulse * 0.72 + sweepPulse * 0.55));
                half alpha = saturate(max(source.a * (half)mask, (half)mask) * _Color.a * input.color.a + (half)((pulse + sweepPulse) * 0.08));
                clip(alpha - max((half)HectonDitherCoverage(input.positionCS.xy), 0.0005h));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
