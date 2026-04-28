Shader "HECTON/Scanner/PulseInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 0.9, 1.0, 0.8)
        _RingThickness ("Ring Thickness", Range(0.001, 0.25)) = 0.05
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float outer = 1.0 - smoothstep(1.0 - _RingThickness, 1.0, radius);
                float inner = smoothstep(max(0.0, 1.0 - _RingThickness * 2.0), 1.0 - _RingThickness, radius);
                float alpha = saturate(outer * inner) * _BaseColor.a;
                return half4(_BaseColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
