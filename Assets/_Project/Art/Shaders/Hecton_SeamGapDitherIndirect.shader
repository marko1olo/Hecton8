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
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 instanceMatrix = _HectonSeamDitherMatrices[input.instanceID];
                float4 instanceColor = _HectonSeamDitherColors[input.instanceID];
                float3 anchorWS = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float distanceFade = saturate(1.0 - distance(anchorWS, _SeamDitherCameraPositionWS) / max(_MaxCameraDistance, 0.001));
                float4 positionWS = mul(instanceMatrix, input.positionOS);

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.uv = input.uv;
                output.color = instanceColor * _BaseTint;
                output.color.a *= distanceFade;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = pow(radial, _Softness) * input.color.a;
                return half4(input.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
