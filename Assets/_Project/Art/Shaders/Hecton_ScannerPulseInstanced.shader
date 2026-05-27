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
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+10"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

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
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float TemporalFlicker01(float timeSeconds, float speed, float phaseOffset)
            {
                return Hash21(float2(timeSeconds * max(speed, 0.001), phaseOffset));
            }

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) * rcp(max(edge1 - edge0, 0.0001)));
            }

            float2 ApproximateUnitDirectionDiamond(float2 value)
            {
                float2 absValue = abs(value);
                float invRadius = rcp(max(absValue.x + absValue.y, 0.0001));
                return value * invRadius;
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                float radiusSq = dot(centered, centered);
                float outerStart = 1.0 - _RingThickness;
                float innerStart = max(0.0, 1.0 - _RingThickness * 2.0);
                outerStart *= outerStart;
                innerStart *= innerStart;
                float outer = 1.0 - ResolveLinearRamp01(outerStart, 1.0, radiusSq);
                float inner = ResolveLinearRamp01(innerStart, outerStart, radiusSq);
                float radial01 = saturate(radiusSq);
                float band = floor(radial01 * 42.0 + _Time.y * 18.0);
                float noise = Hash21(float2(band, floor(centered.x * 19.0 + centered.y * 23.0)));
                float sweep = ResolveLinearRamp01(0.92, 1.0, frac(radial01 * 6.0 - _Time.y * 1.7));
                float analogJitter = lerp(1.0, 0.72 + noise * 0.56, saturate(_AnalogJitterStrength + sweep * 0.08));
                float chromaBias = (noise - 0.5) * _AnalogJitterStrength;
                float alpha = saturate(outer * inner * analogJitter) * _BaseColor.a;
                float2 radialDir = ApproximateUnitDirectionDiamond(centered);
                float sweepDot = dot(radialDir, float2(_SinTime.y, _CosTime.y));
                float sweepLine = saturate(1.0 - abs(sweepDot - 0.91) * 16.0);
                sweepLine *= sweepLine;
                float sweepFlicker = TemporalFlicker01(_Time.y, 24.0, band * 0.173 + noise * 5.13);
                float sweepGlow = sweepLine * sweepFlicker * _SweepInterferenceStrength;
                alpha = saturate(alpha + sweepGlow * outer * _BaseColor.a);
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.0005));
                float3 color = _BaseColor.rgb + float3(chromaBias * 0.15, chromaBias * 0.05, -chromaBias * 0.08);
                color += _BaseColor.rgb * sweepGlow * 0.6;
                return half4(saturate(color), 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
