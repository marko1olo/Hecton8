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
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB

        Pass
        {
            Name "DataRecPulse"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
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

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                half pulse = lerp(_PulseMin, _PulseMax, 0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half2 centeredUv = (half2)(input.texcoord - 0.5);
                half radiusSq = max(dot(centeredUv, centeredUv), 0.0001h);
                half radialMask = saturate(1.0h - abs(radiusSq - 0.1024h) * 16.0h);
                half phase = _Time.y * _PulseSpeed * 0.42h;
                half sweepSin;
                half sweepCos;
                sincos(phase, sweepSin, sweepCos);
                half2 dir = centeredUv * rsqrt(radiusSq);
                half2 sweepDir = half2(sweepCos, sweepSin);
                half sweepWave = 0.5h + 0.5h * dot(dir, sweepDir);
                half sweep = smoothstep(0.82h, 1.0h, sweepWave) * radialMask * _SweepIntensity;
                half4 color = texel * input.color;
                color.a *= saturate(pulse + sweep);
                color.rgb *= lerp(0.72h, 1.35h + sweep, pulse);
                return color;
            }
            ENDHLSL
        }
    }
}
