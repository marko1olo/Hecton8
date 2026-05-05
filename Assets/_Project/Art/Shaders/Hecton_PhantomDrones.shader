Shader "Hecton8/VFX/PhantomDrones"
{
    Properties
    {
        _BaseTint ("Base Tint", Color) = (0.10, 0.85, 1.00, 0.85)
        _EdgeBoost ("Edge Boost", Range(0, 4)) = 1.7
        _DistanceFadeStart ("Distance Fade Start", Float) = 45.0
        _DistanceFadeEnd ("Distance Fade End", Float) = 92.0
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

            StructuredBuffer<float4x4> _PhantomMatrices;
            StructuredBuffer<float4> _PhantomColors;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float _EdgeBoost;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float4 color : COLOR0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 instanceMatrix = _PhantomMatrices[input.instanceID];
                float4 positionWS = mul(instanceMatrix, input.positionOS);
                float3 normalWS = normalize(mul((float3x3)instanceMatrix, input.normalOS));
                float distanceToCamera = distance(positionWS.xyz, _WorldSpaceCameraPos);
                float distanceFade = 1.0 - smoothstep(_DistanceFadeStart, max(_DistanceFadeEnd, _DistanceFadeStart + 0.001), distanceToCamera);

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.normalWS = normalWS;
                output.viewDirWS = normalize(_WorldSpaceCameraPos.xyz - positionWS.xyz);
                output.color = _PhantomColors[input.instanceID] * _BaseTint;
                output.color.a *= distanceFade;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half rim = (half)pow(saturate(1.0 - abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)))), _EdgeBoost);
                half emission = saturate(input.color.a + rim);
                return half4(input.color.rgb * emission, emission);
            }
            ENDHLSL
        }
    }
}
