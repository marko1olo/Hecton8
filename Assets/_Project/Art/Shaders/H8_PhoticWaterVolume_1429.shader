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
                half alpha = saturate(color.a * _Alpha * input.color.a * noise);
                return half4(color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
