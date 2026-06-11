Shader "HECTON/Environment/RuinSeepSheen"
{
    Properties
    {
        [MainTexture] _MainTex ("Seep Mask", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (0.48, 0.68, 0.62, 0.32)
        [HDR] _HighlightColor ("Highlight", Color) = (0.86, 0.94, 0.90, 1.0)
        _Opacity ("Opacity", Range(0, 2)) = 1
        _LuminanceBias ("Luminance Bias", Range(0, 1)) = 0.12
        _LuminancePower ("Luminance Power", Range(0.25, 4)) = 1.6
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.24
        _FlowSpeed ("Flow Speed", Range(0, 0.2)) = 0.018
        _FlowAmount ("Flow Amount", Range(0, 1)) = 0.28
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.4
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.85
        _DitherScale ("Dither Scale", Range(1, 8)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "RuinSeepSheen"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half4 _HighlightColor;
                half _Opacity;
                half _LuminanceBias;
                half _LuminancePower;
                half _EdgeSoftness;
                half _FlowSpeed;
                half _FlowAmount;
                half _FresnelPower;
                half _FresnelStrength;
                half _DitherScale;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half FastMaskPower(half value, half power)
            {
                half v2 = value * value;
                half v4 = v2 * v2;
                half low = lerp(value, v2, saturate(power - 1.0h));
                half high = lerp(v2, v4, saturate((power - 2.0h) * 0.5h));
                return power < 2.0h ? low : high;
            }

            half DeriveMask(float2 uv)
            {
                half3 sampleRgb = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half luminance = dot(sampleRgb, half3(0.2126h, 0.7152h, 0.0722h));
                half mask = saturate((luminance - _LuminanceBias) / max(_EdgeSoftness, 0.001h));
                return FastMaskPower(mask, max(_LuminancePower, 0.001h));
            }

            float ResolveInterleavedGradientNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS / max(_DitherScale, 1.0h));
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUv = input.uv;
                float2 flowUv = baseUv + float2(0.0, -_Time.y * _FlowSpeed);
                half baseMask = DeriveMask(baseUv);
                half flowMask = DeriveMask(flowUv);
                half seepMask = saturate(lerp(baseMask, flowMask, _FlowAmount));

                float3 viewDirWS = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float3 normalWS = SafeNormalize(input.normalWS);
                half fresnel = FastMaskPower(1.0h - saturate(dot(normalWS, viewDirWS)), max(_FresnelPower, 0.001h)) * _FresnelStrength;
                half highlight = saturate(0.55h + fresnel);

                half3 color = lerp(_TintColor.rgb, _HighlightColor.rgb, saturate(fresnel)) * highlight;
                half alpha = saturate(seepMask * _TintColor.a * _Opacity * (0.78h + fresnel * 0.4h));
                clip(alpha - ResolveInterleavedGradientNoise(input.positionCS.xy));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
