Shader "Hecton8/World/SurfaceFoamBlob1447"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.93, 1.0, 0.92, 0.58)
        _NoiseScale ("Noise Scale", Range(1.0, 96.0)) = 28.0
        _Breakup ("Breakup", Range(0.0, 1.0)) = 0.36
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+32"
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
                p = frac(p * float2(127.13, 311.77));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 d = input.uv * 2.0h - 1.0h;
                half radial = saturate(1.0h - dot(d, d));
                half cell = Hash21(floor(input.uv * _NoiseScale));
                half vein = 0.5h + 0.5h * sin((input.uv.x * 23.0h) + (input.uv.y * 17.0h) + cell * 6.2831h);
                half organic = saturate((cell * 0.58h + vein * 0.42h) - _Breakup);
                half alpha = pow(radial, 1.55h) * smoothstep(0.0h, 0.42h, organic) * input.color.a * _Tint.a;
                half edgeTint = saturate(0.55h + radial * 0.45h);
                return half4(_Tint.rgb * edgeTint, alpha);
            }
            ENDHLSL
        }
    }
}
