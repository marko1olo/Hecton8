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
            #pragma multi_compile_fog

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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
                float distanceToCamera : TEXCOORD1;
                half fade : TEXCOORD2;
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

            Varyings Vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                float3 positionWS = mul(_HectonHLODInstanceMatrices[instanceID], float4(input.positionOS.xyz, 1.0)).xyz + _GlobalFloatingOffset.xyz;
                float4 positionCS = TransformWorldToHClip(positionWS);
                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                output.distanceToCamera = distance(positionWS, _WorldSpaceCameraPos);
                output.fade = saturate(_HectonHLODInstanceFade[instanceID].x);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half visibility = saturate((input.distanceToCamera - _VisibilityStart) / max(1.0, _VisibilityEnd - _VisibilityStart));
                half fogFade = saturate(1.0h - input.fogFactor * 0.72h);
                float ditherThreshold = ResolveBayer4x4(input.positionCS.xy);
                clip(input.fade - ditherThreshold);

                half intensity = _SilhouetteColor.a * visibility * fogFade;
                return half4(_SilhouetteColor.rgb * intensity, 1.0h);
            }
            ENDHLSL
        }
    }
}
