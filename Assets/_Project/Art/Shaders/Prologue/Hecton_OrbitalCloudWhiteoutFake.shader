Shader "Hecton/Prologue/Orbital Cloud Whiteout Fake"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (0.92, 0.96, 1.0, 1.0)
        _NoiseScale ("Noise Scale", Float) = 7.0
        _Alpha ("Alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+80" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OrbitalWhiteoutFake"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColor;
                half _NoiseScale;
                half _Alpha;
            CBUFFER_END

            float _H8OrbitalCloudWhiteout;
            float _H8OrbitalUniverseSpeed;
            float _H8OrbitalMathLod;

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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half whiteout = saturate(_H8OrbitalCloudWhiteout);
                half speedNoise = saturate(_H8OrbitalUniverseSpeed * 0.0002h);
                half noise = 0.92h;
                if (_H8OrbitalMathLod >= 0.5)
                {
                    half n0 = saturate(sin((input.uv.x + _Time.y * 0.14h) * _NoiseScale * 19.1h) * 0.5h + 0.5h);
                    half n1 = saturate(sin((input.uv.y - _Time.y * 0.19h) * _NoiseScale * 13.7h) * 0.5h + 0.5h);
                    noise = lerp(0.72h, 1.0h, n0 * n1);
                }
                half overkill = _H8OrbitalMathLod > 2.5 ? 1.08h : 1.0h;
                half alpha = saturate(whiteout * _Alpha * lerp(0.84h, 1.0h, speedNoise) * noise * overkill);
                return half4(_CloudColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
