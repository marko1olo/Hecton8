Shader "HECTON/Sky/H8_SurfaceCloudPanorama_1428"
{
    Properties
    {
        _CloudTexA ("Primary Cloud Texture", 2D) = "gray" {}
        _CloudTexB ("Shear Cloud Texture", 2D) = "gray" {}
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.20, 0.34, 0.48, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.68, 0.72, 0.82, 1)
        [HDR] _CloudLit ("Cloud Lit", Color) = (0.86, 0.90, 0.96, 1)
        [HDR] _CloudShadow ("Cloud Shadow", Color) = (0.22, 0.28, 0.38, 1)
        _CloudThreshold ("Cloud Threshold", Range(0, 1)) = 0.34
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.6)) = 0.24
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.74
        _HorizonMist ("Horizon Mist", Range(0, 1)) = 0.35
        _Exposure ("Exposure", Range(0.1, 3.0)) = 1.12
        _FlowA ("Flow A", Vector) = (0.010, 0.002, 1.7, 0.75)
        _FlowB ("Flow B", Vector) = (-0.006, 0.001, 2.4, 0.62)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background+8"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SurfaceCloudPanorama"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTexA); SAMPLER(sampler_CloudTexA);
            TEXTURE2D(_CloudTexB); SAMPLER(sampler_CloudTexB);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTexA_ST;
                float4 _CloudTexB_ST;
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _CloudLit;
                half4 _CloudShadow;
                half _CloudThreshold;
                half _CloudSoftness;
                half _CloudOpacity;
                half _HorizonMist;
                half _Exposure;
                float4 _FlowA;
                float4 _FlowB;
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

            half Luma(half3 value)
            {
                return dot(value, half3(0.299h, 0.587h, 0.114h));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                half vertical = saturate((half)uv.y);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0.02h, 1.0h, vertical));

                float2 flowA = uv * _FlowA.zw + _Time.y * _FlowA.xy;
                float2 flowB = uv * _FlowB.zw + _Time.y * _FlowB.xy + float2(0.21, 0.37);
                half3 cloudA = (half3)SAMPLE_TEXTURE2D(_CloudTexA, sampler_CloudTexA, flowA).rgb;
                half3 cloudB = (half3)SAMPLE_TEXTURE2D(_CloudTexB, sampler_CloudTexB, flowB).rgb;
                half cloudSignal = saturate(Luma(cloudA) * 0.72h + Luma(cloudB) * 0.46h);
                half cloudMask = smoothstep(_CloudThreshold, _CloudThreshold + _CloudSoftness, cloudSignal);
                half horizonFade = smoothstep(0.05h, 0.34h, vertical);
                half highFade = 1.0h - smoothstep(0.92h, 1.0h, vertical);
                cloudMask *= horizonFade * highFade * _CloudOpacity;

                half detail = saturate((Luma(cloudA) - Luma(cloudB)) * 0.5h + 0.5h);
                half3 cloudColor = lerp(_CloudShadow.rgb, _CloudLit.rgb, detail);
                half3 color = lerp(sky, cloudColor, cloudMask);

                half mist = (1.0h - smoothstep(0.08h, 0.26h, vertical)) * _HorizonMist;
                color = lerp(color, _HorizonColor.rgb, mist);
                return half4(color * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
