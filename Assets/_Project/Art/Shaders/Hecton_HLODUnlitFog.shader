Shader "Hidden/Hecton8/World/HLODUnlitFog"
{
    Properties
    {
        _SilhouetteColor ("Silhouette Color", Color) = (0.02, 0.025, 0.03, 0.58)
        _VisibilityStart ("Visibility Start", Range(1, 4000)) = 300
        _VisibilityEnd ("Visibility End", Range(1, 12000)) = 3200
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+18"
        }

        Pass
        {
            Name "HLOD"

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SilhouetteColor;
                float _VisibilityStart;
                float _VisibilityEnd;
                float4 _GlobalFloatingOffset;
            CBUFFER_END

            StructuredBuffer<float4x4> _HectonHLODInstanceMatrices;
            StructuredBuffer<float4> _HectonHLODInstanceFade;

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
                float distanceToCamera : TEXCOORD1;
                half fade : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float ResolveBayer4x4(float2 pixelPosition)
            {
                uint2 pixel = uint2(pixelPosition) & 3u;
                static const float kBayer4x4[16] =
                {
                    0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                    3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                    15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
                };

                return kBayer4x4[pixel.x + (pixel.y << 2)];
            }

            half QuantizeDitherAlpha4(half alpha)
            {
                return saturate((half)floor(saturate(alpha) * 4.0h + 0.999h) * 0.25h);
            }

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
                float3 positionWS = mul(_HectonHLODInstanceMatrices[instanceID], float4(input.positionOS.xyz, 1.0)).xyz + _GlobalFloatingOffset.xyz;
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                float3 cameraDelta = positionWS - _WorldSpaceCameraPos;
                output.distanceToCamera = dot(cameraDelta, cameraDelta);
                output.fade = saturate(_HectonHLODInstanceFade[instanceID].x);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float visibilityStartSq = _VisibilityStart * _VisibilityStart;
                float visibilityEndSq = max(visibilityStartSq + 1.0, _VisibilityEnd * _VisibilityEnd);
                half visibility = saturate((input.distanceToCamera - visibilityStartSq) / max(1.0, visibilityEndSq - visibilityStartSq));
                half fogFade = saturate(1.0h - input.fogFactor * 0.72h);
                float ditherThreshold = ResolveBayer4x4(input.positionCS.xy);
                half quantizedFade = QuantizeDitherAlpha4(input.fade);
                clip(quantizedFade - ditherThreshold - 0.0001h);

                half intensity = _SilhouetteColor.a * visibility * fogFade;
                return half4(_SilhouetteColor.rgb * intensity, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
