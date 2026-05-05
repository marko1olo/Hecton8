Shader "Hecton8/UI/CompassRibbon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Compass Ribbon", 2D) = "white" {}
        _Color ("Tint", Color) = (0.58, 0.96, 1.0, 0.72)
        _CompassOffset ("Compass Offset", Range(0, 1)) = 0
        _TickDensity ("Tick Density", Range(8, 128)) = 48
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.18
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.12
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
            Name "CompassRibbon"

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
                float4 _Color;
                float _CompassOffset;
                float _TickDensity;
                float _ScanlineStrength;
                float _PulseStrength;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float scrollX = frac(uv.x + _CompassOffset);
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(scrollX, uv.y));

                float minorTick = 1.0 - smoothstep(0.010, 0.026, abs(frac(scrollX * _TickDensity) - 0.5));
                float majorTick = 1.0 - smoothstep(0.012, 0.040, abs(frac(scrollX * 4.0) - 0.5));
                float centerBand = smoothstep(0.18, 0.42, uv.y) * (1.0 - smoothstep(0.58, 0.82, uv.y));
                float centerNotch = 1.0 - smoothstep(0.015, 0.055, abs(uv.x - 0.5));
                float scanline = lerp(1.0, 0.82 + 0.18 * step(0.5, frac(uv.y * 96.0 + _Time.y)), _ScanlineStrength);
                float mask = saturate((minorTick * 0.24 + majorTick * 0.78 + centerNotch * 0.7) * centerBand);
                float pulseWindow = 1.0 - smoothstep(0.0, 0.42, abs(uv.x - 0.5));
                float pulse = (0.5 + 0.5 * sin(_Time.y * 5.7 + scrollX * 37.0)) * pulseWindow * _PulseStrength;
                float sweepCenter = frac(_Time.y * 0.17 + _CompassOffset);
                float sweepDelta = abs(frac((uv.x - sweepCenter) + 0.5) - 0.5);
                float sweepPulse = (1.0 - smoothstep(0.0, 0.065, sweepDelta)) * centerBand * _PulseStrength;

                half3 color = max(source.rgb * source.a * (half)mask, _Color.rgb * (half)mask) * (half)scanline;
                color = saturate(color + _Color.rgb * (half)(pulse * 0.72 + sweepPulse * 0.55));
                half alpha = saturate(max(source.a * (half)mask, (half)mask) * _Color.a * input.color.a + (half)((pulse + sweepPulse) * 0.08));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
