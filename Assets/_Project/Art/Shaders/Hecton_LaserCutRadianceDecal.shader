Shader "HECTON/World/LaserCutRadianceDecal"
{
    Properties
    {
        _Color("Color", Color) = (1.0, 0.42, 0.12, 1.0)
        _CoreRadius("Core Radius", Range(0.05, 0.95)) = 0.22
        _Intensity("Intensity", Range(0.0, 16.0)) = 6.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "LaserCutRadiance"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _CoreRadius;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half ResolveInstanceFade()
            {
                float3 zAxis = float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22);
                return saturate((half)dot(zAxis, zAxis));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.fade = ResolveInstanceFade();
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half2 centeredUv = input.uv * 2.0h - 1.0h;
                half radialSq = saturate(1.0h - dot(centeredUv, centeredUv));
                half core = smoothstep(_CoreRadius, 1.0h, radialSq);
                half edge = radialSq * radialSq;
                half alpha = saturate((core + edge) * input.fade);
                return half4(_Color.rgb * (_Intensity * alpha), alpha);
            }
            ENDHLSL
        }
    }
}
