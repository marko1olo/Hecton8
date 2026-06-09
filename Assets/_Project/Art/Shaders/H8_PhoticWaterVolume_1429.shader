Shader "HECTON/World/H8_PhoticWaterVolume_1429"
{
    Properties
    {
        [HDR] _NearColor ("Near Color", Color) = (0.20, 0.78, 0.82, 0.18)
        [HDR] _FarColor ("Far Color", Color) = (0.04, 0.30, 0.34, 0.34)
        _Alpha ("Alpha", Range(0, 1)) = 0.34
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 4.6
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.42
        _VerticalFade ("Vertical Fade", Range(0.2, 6)) = 1.7
        _Flow ("Flow", Vector) = (0.018, 0.006, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+8"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "PhoticWaterVolume"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _NearColor;
                half4 _FarColor;
                half _Alpha;
                half _NoiseScale;
                half _NoiseStrength;
                half _VerticalFade;
                float4 _Flow;
            CBUFFER_END

            float4 _HectonCelestialLightReadability0; // x depth, y direct sun, z ambient readability, w visibility meters
            float4 _HectonCelestialLightReadability1; // x mesophotic, y deep darkness, z artificial light, w biolum
            float4 _HectonCelestialLightReadability2; // x caustic, y fog, z scattering, w exposure
            float4 _HectonCelestialLightReadability3; // x stratum, y flags, z sequence, w black-crush floor

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 flowUv = input.uv * _NoiseScale + _Time.y * _Flow.xy;
                half n = (half)ValueNoise(flowUv);
                half n2 = (half)ValueNoise(flowUv * 2.13 + float2(4.7, 1.9));
                half noise = saturate(lerp(1.0h, n * 0.63h + n2 * 0.37h, _NoiseStrength));
                half depthFade = saturate(pow((half)input.uv.y, _VerticalFade));
                half4 color = lerp(_NearColor, _FarColor, depthFade);
                half lightSignal = (half)(
                    abs(_HectonCelestialLightReadability0.x) +
                    abs(_HectonCelestialLightReadability0.w) +
                    abs(_HectonCelestialLightReadability1.y) +
                    abs(_HectonCelestialLightReadability2.y) +
                    abs(_HectonCelestialLightReadability3.z));
                half lightKnown = step(0.0001h, lightSignal);
                half visibility01 = (half)saturate(_HectonCelestialLightReadability0.w / 112.0);
                half deepDarkness = (half)saturate(_HectonCelestialLightReadability1.y);
                half fogPressure = (half)saturate((_HectonCelestialLightReadability2.y - 0.72) * 0.24271844);
                half scattering = (half)saturate(_HectonCelestialLightReadability2.z * 0.3125);
                half playableFloor = max(0.055h, (half)_HectonCelestialLightReadability3.w);
                half hazePressure = saturate(max(fogPressure, deepDarkness * 0.74h) * (1.0h - visibility01 * 0.28h));
                half3 abyssTint = max(_FarColor.rgb * (0.62h + scattering * 0.45h), half3(playableFloor, playableFloor * 1.28h, playableFloor * 1.48h));
                color.rgb = lerp(color.rgb, lerp(color.rgb, abyssTint, hazePressure), lightKnown);
                half alphaScale = lerp(1.0h, saturate(0.78h + hazePressure * 0.52h), lightKnown);
                half alpha = min(0.82h, saturate(color.a * _Alpha * input.color.a * noise * alphaScale));
                return half4(color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
