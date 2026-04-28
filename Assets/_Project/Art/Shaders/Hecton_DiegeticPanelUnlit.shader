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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float4 _Color;
                float4 _BaseMap_ST;
                float _PanelPowerLevel;
                float _DepthFadeRange;
                float _OcclusionActive;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 screenSample = max(baseSample, mainSample);
                float powerLevel = saturate(_PanelPowerLevel);
                float3 emissive = screenSample.rgb * _Color.rgb * lerp(0.45, 1.0, powerLevel);
                return half4(emissive, screenSample.a * _Color.a);
            }
            ENDHLSL
        }
    }
}
