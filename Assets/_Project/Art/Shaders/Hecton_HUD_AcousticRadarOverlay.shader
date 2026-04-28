Shader "Hecton8/UI/AcousticRadarOverlay"
{
    Properties
    {
        _AcousticRadarTex ("Acoustic Radar", 2D) = "black" {}
        _PrimaryColor ("Primary Color", Color) = (0.55, 0.9, 1.0, 1.0)
        _WarningColor ("Warning Color", Color) = (1.0, 0.28, 0.32, 1.0)
        _OverlayOpacity ("Overlay Opacity", Range(0, 1)) = 0.18
        _InnerEdge ("Inner Edge", Range(0, 1)) = 0.74
        _BandThickness ("Band Thickness", Range(0.01, 0.5)) = 0.18
        _WaveAmplitude ("Wave Amplitude", Range(0, 8)) = 2.4
        _PulseFrequency ("Pulse Frequency", Range(0, 16)) = 3.2
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0.2
        _RadarIntensity ("Radar Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AcousticRadarOverlay"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _PrimaryColor;
                float4 _WarningColor;
                float _OverlayOpacity;
                float _InnerEdge;
                float _BandThickness;
                float _WaveAmplitude;
                float _PulseFrequency;
                float _GlitchAmount;
                float _RadarIntensity;
            CBUFFER_END

            TEXTURE2D(_AcousticRadarTex);
            SAMPLER(sampler_AcousticRadarTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = (input.uv * 2.0) - 1.0;
                float radial = length(centered);
                float angle01 = frac((atan2(centered.y, centered.x) * (1.0 / TWO_PI)) + 0.5);
                float intensity = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(angle01, 0.5)).r;
                float neighbourA = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(frac(angle01 + 0.004), 0.5)).r;
                float neighbourB = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(frac(angle01 - 0.004), 0.5)).r;
                intensity = saturate(max(intensity, max(neighbourA, neighbourB)));

                float dynamicBandThickness = _BandThickness + (intensity * 0.09) + (_RadarIntensity * 0.04);
                float outerEdge = saturate(_InnerEdge + dynamicBandThickness);
                float edgeMask = smoothstep(_InnerEdge, _InnerEdge + 0.04, radial) *
                                 (1.0 - smoothstep(outerEdge, min(0.999, outerEdge + 0.14 + intensity * 0.05), radial));
                if (edgeMask <= 0.0001)
                    return 0;

                float timeValue = _Time.y;
                float wave = 0.5 + (0.5 * sin((angle01 * TWO_PI * 10.0) + (timeValue * _PulseFrequency * TWO_PI)));
                float sweep = 0.5 + (0.5 * sin((radial * 30.0) - (timeValue * 7.5) + (angle01 * TWO_PI * 4.0)));
                float glitchSeed = Hash21(floor(input.uv * float2(160.0, 96.0)) + floor(timeValue * 12.0));
                float glitch = smoothstep(0.78, 1.0, glitchSeed) * _GlitchAmount;
                float blipScale = 1.0 + (intensity * 1.4);
                float blip = saturate(intensity * (0.5 + (wave * 0.8 * blipScale)) + (_RadarIntensity * 0.2 * sweep) + glitch * 0.18);

                float3 color = lerp(_PrimaryColor.rgb, _WarningColor.rgb, saturate(intensity * 1.35 + glitch * 0.4));
                float alpha = saturate(_OverlayOpacity * edgeMask * blip * (0.82 + intensity * 0.85)) * input.color.a;
                float glow = 0.55 + (intensity * 1.55) + (sweep * 0.18) + (glitch * 0.35);
                return half4(color * glow, alpha);
            }
            ENDHLSL
        }
    }
}
