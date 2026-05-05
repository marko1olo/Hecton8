Shader "HECTON/Scanner/PulseInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 0.9, 1.0, 0.8)
        _RingThickness ("Ring Thickness", Range(0.001, 0.25)) = 0.05
        _AnalogJitterStrength ("Analog Jitter Strength", Range(0.0, 0.35)) = 0.12
        _SweepInterferenceStrength ("Sweep Interference Strength", Range(0.0, 0.25)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+10"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RingThickness;
                float _AnalogJitterStrength;
                float _SweepInterferenceStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float outer = 1.0 - smoothstep(1.0 - _RingThickness, 1.0, radius);
                float inner = smoothstep(max(0.0, 1.0 - _RingThickness * 2.0), 1.0 - _RingThickness, radius);
                float band = floor(radius * 42.0 + _Time.y * 18.0);
                float noise = Hash21(float2(band, floor(centered.x * 19.0 + centered.y * 23.0)));
                float sweep = smoothstep(0.92, 1.0, frac(radius * 6.0 - _Time.y * 1.7));
                float analogJitter = lerp(1.0, 0.72 + noise * 0.56, saturate(_AnalogJitterStrength + sweep * 0.08));
                float chromaBias = (noise - 0.5) * _AnalogJitterStrength;
                float alpha = saturate(outer * inner * analogJitter) * _BaseColor.a;
                float2 radialDir = centered * rsqrt(max(dot(centered, centered), 0.0001));
                float sweepDot = dot(radialDir, float2(_SinTime.y, _CosTime.y));
                float sweepLine = saturate(1.0 - abs(sweepDot - 0.91) * 16.0);
                sweepLine = sweepLine * sweepLine * (3.0 - 2.0 * sweepLine);
                float sweepFlicker = Hash21(float2(floor(_Time.y * 24.0), band + 91.0));
                float sweepGlow = sweepLine * sweepFlicker * _SweepInterferenceStrength;
                alpha = saturate(alpha + sweepGlow * outer * _BaseColor.a);
                float3 color = _BaseColor.rgb + float3(chromaBias * 0.15, chromaBias * 0.05, -chromaBias * 0.08);
                color += _BaseColor.rgb * sweepGlow * 0.6;
                return half4(saturate(color) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
