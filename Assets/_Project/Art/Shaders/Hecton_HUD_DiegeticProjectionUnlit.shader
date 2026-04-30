Shader "Hecton8/UI/HUDDiegeticProjectionUnlit"
{
    Properties
    {
        _BaseMap ("HUD Render Texture", 2D) = "black" {}
        _MainTex ("HUD Main Texture", 2D) = "black" {}
        _Color ("HUD Tint", Color) = (0.78, 1.0, 0.94, 0.96)
        _Intensity ("HUD Intensity", Range(0, 8)) = 2.2
        _AlphaGain ("RGB Alpha Gain", Range(0, 8)) = 3.0
        _BlackCutoff ("Black Cutoff", Range(0, 0.25)) = 0.015
        _EdgeFade ("Panel Edge Fade", Range(0, 0.5)) = 0.06
        _FrameAlpha ("Physical Frame Alpha", Range(0, 0.5)) = 0.12
        _PanelPowerLevel ("Panel Power", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+80"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "HUDDiegeticProjection"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

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
                float4 _Color;
                float _Intensity;
                float _AlphaGain;
                float _BlackCutoff;
                float _EdgeFade;
                float _FrameAlpha;
                float _PanelPowerLevel;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 hudSample = max(baseSample, mainSample);

                float maxRgb = max(max(hudSample.r, hudSample.g), hudSample.b);
                float rgbAlpha = saturate((maxRgb - _BlackCutoff) * _AlphaGain);
                float2 centerUv = abs(input.uv - 0.5) * 2.0;
                float edge = max(centerUv.x, centerUv.y);
                float edgeFade = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, edge);
                float frameMask = step(0.982, edge);
                float alpha = max(hudSample.a, rgbAlpha) * _Color.a * saturate(_PanelPowerLevel) * edgeFade;
                float3 color = hudSample.rgb * _Color.rgb * _Intensity * lerp(0.45, 1.0, saturate(_PanelPowerLevel));
                color += _Color.rgb * frameMask * _FrameAlpha;
                alpha = max(alpha, frameMask * _FrameAlpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
