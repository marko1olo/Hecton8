Shader "HECTON/Sky/H8_AtmosphericCloudSheet_1428"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "gray" {}
        [HDR] _Tint ("Cloud Tint", Color) = (0.80, 0.84, 0.92, 1)
        [HDR] _ShadowTint ("Cloud Shadow Tint", Color) = (0.22, 0.29, 0.36, 1)
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.46
        _Threshold ("Density Threshold", Range(0.0, 1.0)) = 0.34
        _Softness ("Edge Softness", Range(0.01, 0.75)) = 0.24
        _Contrast ("Contrast", Range(0.1, 3.0)) = 1.18
        _FlowSpeed ("Flow Speed", Vector) = (0.004, 0.0015, 0, 0)
        _SecondaryFlow ("Secondary Flow", Vector) = (-0.0012, 0.0022, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-80"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        LOD 50

        Pass
        {
            Name "CloudSheetForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _ShadowTint;
                half _Alpha;
                half _Threshold;
                half _Softness;
                half _Contrast;
                float4 _FlowSpeed;
                float4 _SecondaryFlow;
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
                half heightFade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.heightFade = saturate(input.uv.y);
                return output;
            }

            half Luma(half3 value)
            {
                return dot(value, half3(0.299h, 0.587h, 0.114h));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _Time.y;
                float2 uv0 = input.uv + _FlowSpeed.xy * time;
                float2 uv1 = input.uv * 1.73 + _SecondaryFlow.xy * time;

                half n0 = Luma((half3)SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv0).rgb);
                half n1 = Luma((half3)SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1).rgb);
                half density = saturate((n0 * 0.72h + n1 * 0.28h - 0.5h) * _Contrast + 0.5h);
                half mask = smoothstep(_Threshold, _Threshold + _Softness, density);

                half horizonFade = smoothstep(0.02h, 0.18h, input.heightFade);
                half alpha = saturate(mask * _Alpha * horizonFade);
                half3 color = lerp(_ShadowTint.rgb, _Tint.rgb, saturate(density * 1.2h));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
