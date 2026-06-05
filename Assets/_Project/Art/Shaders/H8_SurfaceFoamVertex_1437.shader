Shader "Hecton8/World/SurfaceFoamVertex1437"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.92, 1.0, 0.94, 0.72)
        _NoiseScale ("Noise Scale", Range(1.0, 96.0)) = 24.0
        _Breakup ("Breakup", Range(0.0, 1.0)) = 0.32
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _NoiseScale;
                half _Breakup;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half grain = Hash21(floor(input.uv * _NoiseScale));
                half streak = 0.5h + 0.5h * sin((input.uv.y * 21.0h) + (grain * 6.2831h));
                half breakup = smoothstep(_Breakup, 1.0h, grain * 0.72h + streak * 0.48h);
                half edge = saturate(1.0h - abs(input.uv.x * 2.0h - 1.0h));
                half alpha = input.color.a * _Tint.a * breakup * smoothstep(0.08h, 0.55h, edge);
                return half4(_Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
