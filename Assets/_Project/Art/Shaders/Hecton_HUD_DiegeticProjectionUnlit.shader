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

                if (_PanelPowerLevel < 0.1)
                {
                    float dither = Bayer4x4(floor(input.positionCS.xy));
                    float luminance = dot(hudSample.rgb, float3(0.2126, 0.7152, 0.0722)) * max(_Intensity, 0.001);
                    float phosphorBit = step(0.13 + dither * 0.58, luminance);
                    float frameBit = frameMask * 0.42;
                    float phosphorJitter = lerp(0.72, 1.0, dither);
                    color = float3(0.02, 0.92, 0.24) * (phosphorBit * phosphorJitter + frameBit);
                    alpha = max(alpha, saturate((phosphorBit * max(rgbAlpha, hudSample.a) + frameBit) * edgeFade * _Color.a));
                }

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
