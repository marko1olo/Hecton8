Shader "HECTON/World/H8_SurfaceHorizonHaze_1428"
{
    Properties
    {
        [HDR] _LowerTint ("Lower Tint", Color) = (0.34, 0.82, 0.84, 0.24)
        [HDR] _UpperTint ("Upper Tint", Color) = (0.78, 0.93, 0.90, 0.18)
        _Alpha ("Alpha", Range(0, 1)) = 0.32
        _LowerFade ("Lower Fade", Range(0, 0.75)) = 0.18
        _UpperFade ("Upper Fade", Range(0.25, 1)) = 0.86
        _Softness ("Softness", Range(0.01, 0.5)) = 0.18
        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.08
        _NoiseScale ("Noise Scale", Range(1, 96)) = 22
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.28
        _GlobalQualityWeight ("Global Quality Weight", Range(0, 1)) = 0.62
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "SurfaceHorizonHaze"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LowerTint;
                half4 _UpperTint;
                half _Alpha;
                half _LowerFade;
                half _UpperFade;
                half _Softness;
                half _EdgeFade;
                half _NoiseScale;
                half _NoiseStrength;
                half _GlobalQualityWeight;
            CBUFFER_END

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

            half Hash21(float2 p)
            {
                p = frac(p * float2(127.13, 311.77));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half quality = saturate(_GlobalQualityWeight);
                half soft = max(_Softness, 0.001h);
                half lower = smoothstep(_LowerFade, saturate(_LowerFade + soft), (half)input.uv.y);
                half upper = 1.0h - smoothstep(saturate(_UpperFade - soft), _UpperFade, (half)input.uv.y);
                half edgeX = min((half)input.uv.x, (half)(1.0 - input.uv.x));
                half edge = smoothstep(0.0h, max(_EdgeFade, 0.001h), edgeX);

                half band = saturate(lower * upper * edge);
                half cellA = Hash21(floor(input.uv * max(_NoiseScale, 1.0h)));
                half cellB = Hash21(floor((input.uv + float2(0.37, 0.11)) * max(_NoiseScale * 0.47h, 1.0h)));
                half drift = 0.5h + 0.5h * sin((input.uv.x * 19.0h) + (input.uv.y * 7.0h) + _Time.y * lerp(0.045h, 0.11h, quality));
                half noise = saturate(cellA * 0.46h + cellB * 0.28h + drift * 0.26h);
                half breakup = lerp(0.92h, lerp(0.78h, 1.14h, noise), saturate(_NoiseStrength * (0.35h + quality * 0.65h)));

                half3 color = lerp(_LowerTint.rgb, _UpperTint.rgb, saturate(input.uv.y));
                half alpha = band * _Alpha * lerp(_LowerTint.a, _UpperTint.a, saturate(input.uv.y)) * breakup;
                alpha *= lerp(0.82h, 1.08h, quality) * input.color.a;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
