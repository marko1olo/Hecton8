Shader "Hecton8/UI/ToolScreenDiegetic"
{
    Properties
    {
        _ToolScreenTex ("Tool Screen Texture", 2D) = "black" {}
        _BaseMap ("Base Map", 2D) = "black" {}
        _MainTex ("Main Tex", 2D) = "black" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _Color ("Tint", Color) = (0.75, 1.0, 0.82, 1.0)
        _FallbackTint ("Fallback Tint", Color) = (0.08, 0.55, 0.18, 1.0)
        _ToolFallback01 ("Fallback", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        ZTest LEqual
        Blend Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FallbackTint;
                float4 _ToolScreenTex_ST;
                float _ToolFallback01;
            CBUFFER_END

            TEXTURE2D(_ToolScreenTex);
            SAMPLER(sampler_ToolScreenTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            float _ToolHeat01;
            float _ToolBattery01;
            float _ToolDistanceMeters;
            float _ToolAmmoUnits;
            float _ToolCriticalFlash01;
            float _ToolVisualOverkill01;
            float _ToolFault01;
            float _ToolTypeHue01;

            float FastTriangle01(float phase)
            {
                return 1.0 - abs(frac(phase) * 2.0 - 1.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _ToolScreenTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = saturate(input.uv);
                half4 screenSample = SAMPLE_TEXTURE2D(_ToolScreenTex, sampler_ToolScreenTex, uv);

                float3 signal = screenSample.rgb;
                float fallback = saturate(_ToolFallback01);
                float overkill = saturate(_ToolVisualOverkill01) * (1.0 - fallback);
                float scanline = lerp(0.86, 1.10, step(0.5, frac(uv.y * 192.0 + _Time.y * 0.55)));
                float3 color = lerp(signal * _Color.rgb, _FallbackTint.rgb * (0.45 + 0.18 * scanline), fallback);

                float heat = saturate(_ToolHeat01);
                float barBand = step(uv.y, 0.105) * step(0.025, uv.y);
                float barFill = step(uv.x, heat);
                float3 heatColor = lerp(float3(0.0, 1.0, 0.18), float3(1.0, 0.02, 0.0), heat);
                color = lerp(color, heatColor * (1.4 + heat * 1.2), barBand * barFill * (1.0 - fallback));

                float battery = saturate(_ToolBattery01);
                float cellBand = step(0.875, uv.y) * step(uv.y, 0.96);
                float cellFill = step(1.0 - battery, uv.x);
                color += float3(0.04, 0.28, 0.10) * cellBand * cellFill * (1.0 - fallback);

                float grid = step(0.985, frac(uv.x * 16.0)) + step(0.985, frac(uv.y * 12.0));
                float dataSweep = FastTriangle01(uv.x * 2.0 + _Time.y * 0.42);
                color += _Color.rgb * saturate(grid) * overkill * 0.035;
                color += float3(0.05, 0.22, 0.08) * dataSweep * overkill * 0.11;

                float typeHue = saturate(_ToolTypeHue01);
                float3 typeTint = lerp(float3(0.52, 1.0, 0.62), float3(0.42, 0.72, 1.0), typeHue);
                color *= lerp(float3(1.0, 1.0, 1.0), typeTint, overkill * 0.08);

                float fault = saturate(_ToolFault01) * (1.0 - fallback);
                float faultPulse = FastTriangle01(_Time.y * lerp(2.5, 6.0, fault));
                color = lerp(color, float3(1.0, 0.04, 0.0), fault * 0.28);
                color += fault * faultPulse * float3(0.45, 0.02, 0.0);

                float critical = step(0.9, heat) * saturate(_ToolCriticalFlash01);
                float pulse = FastTriangle01(_Time.y * 8.0);
                float invertAmount = critical * lerp(0.35, 1.0, pulse);
                color = lerp(color, 1.0 - color, invertAmount);
                color += critical * pulse * float3(0.75, 0.95, 0.72);

                float edge = 1.0 - smoothstep(0.0, 0.045, min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)));
                color += _Color.rgb * edge * 0.08;

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
