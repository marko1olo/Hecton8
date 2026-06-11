Shader "Hecton8/VFX/SeamGapDitherIndirect"
{
    Properties
    {
        _BaseTint ("Base Tint", Color) = (0.30, 0.90, 1.00, 0.75)
        _Softness ("Softness", Range(0.5, 4.0)) = 2.4
        _MaxCameraDistance ("Max Camera Distance", Float) = 15.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend Off
        ZWrite Off
        Cull Off
        AlphaToMask On

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4x4> _HectonSeamDitherMatrices;
            StructuredBuffer<float4> _HectonSeamDitherColors;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float3 _SeamDitherCameraPositionWS;
                float _Softness;
                float _MaxCameraDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
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
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                float4x4 instanceMatrix = _HectonSeamDitherMatrices[instanceID];
                float4 instanceColor = _HectonSeamDitherColors[instanceID];
                float3 anchorWS = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float maxCameraDistance = max(_MaxCameraDistance, 0.001);
                float3 cameraDelta = anchorWS - _SeamDitherCameraPositionWS;
                float distanceFade = saturate(1.0 - dot(cameraDelta, cameraDelta) / (maxCameraDistance * maxCameraDistance));
                float4 positionWS = mul(instanceMatrix, input.positionOS);

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.uv = input.uv;
                output.color = instanceColor * _BaseTint;
                output.color.a *= distanceFade;
                return output;
            }

            float FastRadialSoftness(float radial, float softness)
            {
                float radial2 = radial * radial;
                float radial4 = radial2 * radial2;
                return lerp(radial, radial4, saturate((softness - 1.0) * 0.3333));
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
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = FastRadialSoftness(radial, _Softness) * input.color.a;
                clip(alpha - max(HectonDitherCoverage(input.positionCS.xy), 0.0005));
                return half4(input.color.rgb * alpha, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
