Shader "Hecton8/UI/DataRecPulse"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,0.08,0.02,1)
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.22
        _PulseMax ("Pulse Max", Range(0, 1)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 32)) = 18.0
        _SweepIntensity ("Sweep Intensity", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend Off
        AlphaToMask On
        ColorMask RGB

        Pass
        {
            Name "DataRecPulse"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _PulseMin;
                half _PulseMax;
                half _PulseSpeed;
                half _SweepIntensity;
            CBUFFER_END

            half FastTrianglePulse01(half phase)
            {
                return 1.0h - abs(frac(phase * 0.15915494h + 0.25h) * 2.0h - 1.0h);
            }

            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                half pulse = lerp(_PulseMin, _PulseMax, FastTrianglePulse01(_Time.y * _PulseSpeed));
                half2 centeredUv = (half2)(input.texcoord - 0.5);
                half radiusSq = max(dot(centeredUv, centeredUv), 0.0001h);
                half radialMask = saturate(1.0h - abs(radiusSq - 0.1024h) * 16.0h);
                half2 dir = centeredUv * rsqrt(radiusSq);
                half sweepPhase = frac(_Time.y * _PulseSpeed * 0.066845h);
                half sweepWave = 1.0h - abs(frac((dir.x * 0.5h + dir.y * 0.5h) + sweepPhase) * 2.0h - 1.0h);
                half sweep = smoothstep(0.82h, 1.0h, sweepWave) * radialMask * _SweepIntensity;
                half4 color = texel * input.color;
                color.a *= saturate(pulse + sweep);
                color.rgb *= lerp(0.72h, 1.35h + sweep, pulse);
                clip(color.a - max((half)InterleavedGradientNoise(floor(input.vertex.xy)), 0.0005h));
                return half4(color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
