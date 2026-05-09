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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
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
            float4 _HectonSonarRadarDistortion;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SquareSaturate(float value)
            {
                float clamped = saturate(value);
                return clamped * clamped;
            }

            float FastTriangleSine01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float ApproximateAngle01(float2 centered)
            {
                float ax = abs(centered.x);
                float ay = abs(centered.y);
                float majorAxis = max(max(ax, ay), 0.00001);
                float octant = min(ax, ay) * rcp(majorAxis) * 0.125;
                float xMajor = step(ay, ax);
                float xPositive = step(0.0, centered.x);
                float yPositive = step(0.0, centered.y);

                float angleXMajorPositive = lerp(1.0 - octant, octant, yPositive);
                float angleXMajorNegative = lerp(0.5 + octant, 0.5 - octant, yPositive);
                float angleXMajor = lerp(angleXMajorNegative, angleXMajorPositive, xPositive);

                float angleYMajorPositive = lerp(0.25 + octant, 0.25 - octant, xPositive);
                float angleYMajorNegative = lerp(0.75 - octant, 0.75 + octant, xPositive);
                float angleYMajor = lerp(angleYMajorNegative, angleYMajorPositive, yPositive);

                return frac(lerp(angleYMajor, angleXMajor, xMajor) + 0.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = (input.uv * 2.0) - 1.0;
                float radialSqr = dot(centered, centered);
                float innerCullSqr = SquareSaturate(max(0.0, _InnerEdge - 0.025));
                float outerCullSqr = SquareSaturate(min(0.999, _InnerEdge + _BandThickness + 0.32));
                if (radialSqr < innerCullSqr || radialSqr > outerCullSqr)
                    return 0;

                float angle01 = ApproximateAngle01(centered);
                float intensity = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(angle01, 0.5)).r;
                float neighbourA = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(frac(angle01 + 0.004), 0.5)).r;
                float neighbourB = SAMPLE_TEXTURE2D(_AcousticRadarTex, sampler_AcousticRadarTex, float2(frac(angle01 - 0.004), 0.5)).r;
                intensity = saturate(max(intensity, max(neighbourA, neighbourB)));

                float dynamicBandThickness = _BandThickness + (intensity * 0.09) + (_RadarIntensity * 0.04);
                float outerEdge = saturate(_InnerEdge + dynamicBandThickness);
                float innerEdgeSqr = SquareSaturate(_InnerEdge);
                float outerEdgeSqr = SquareSaturate(outerEdge);
                float edgeMask = smoothstep(innerEdgeSqr, SquareSaturate(_InnerEdge + 0.04), radialSqr) *
                                 (1.0 - smoothstep(outerEdgeSqr, SquareSaturate(min(0.999, outerEdge + 0.14 + intensity * 0.05)), radialSqr));
                if (edgeMask <= 0.0001)
                    return 0;

                float timeValue = _Time.y;
                float wave = FastTriangleSine01((angle01 * TWO_PI * 10.0) + (timeValue * _PulseFrequency * TWO_PI));
                float sweep = FastTriangleSine01((radialSqr * 30.0) - (timeValue * 7.5) + (angle01 * TWO_PI * 4.0));
                float glitchSeed = Hash21(floor(input.uv * float2(160.0, 96.0)) + floor(timeValue * 12.0));
                float sonarDistortion = saturate(_HectonSonarRadarDistortion.z);
                float screamDistortion = saturate(_HectonSonarRadarDistortion.y);
                float speedDistortion = saturate(_HectonSonarRadarDistortion.x);
                float distortionSeed = Hash21(float2(floor(angle01 * 48.0), floor(radialSqr * 18.0)) + floor(timeValue * lerp(9.0, 22.0, sonarDistortion)));
                float ghostRing = smoothstep(0.83, 1.0, distortionSeed) *
                                  smoothstep(SquareSaturate(_InnerEdge + 0.02), outerEdgeSqr, radialSqr) *
                                  (1.0 - smoothstep(outerEdgeSqr, SquareSaturate(min(0.999, outerEdge + 0.08)), radialSqr));
                float ghostBlip = ghostRing * sonarDistortion * (0.18 + screamDistortion * 0.46 + speedDistortion * 0.24);
                intensity = saturate(max(intensity, ghostBlip));
                float glitch = saturate(smoothstep(0.78, 1.0, glitchSeed) * _GlitchAmount + ghostRing * sonarDistortion * 0.42);
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

    FallBack Off
}
