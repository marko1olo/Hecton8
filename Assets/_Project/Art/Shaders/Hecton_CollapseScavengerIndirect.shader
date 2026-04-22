Shader "Hecton8/World/CollapseScavengerIndirect"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.29, 0.14, 1)
        _AccentColor ("Accent Color", Color) = (0.44, 0.58, 0.26, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4x4> _HectonScavengerMatrices;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AccentColor;
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
                float4 color : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 instanceMatrix = _HectonScavengerMatrices[input.instanceID];
                float3 anchorWS = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float phase = frac((anchorWS.x + anchorWS.z) * 0.17389);
                float wobble = sin(_Time.y * 7.0 + phase * 6.2831853) * 0.025;
                float3 localPosition = input.positionOS.xyz;
                localPosition.y += wobble * saturate(localPosition.z + 0.35);

                float4 positionWS = mul(instanceMatrix, float4(localPosition, 1.0));
                float3 normalWS = normalize(mul((float3x3)instanceMatrix, input.normalOS));

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.normalWS = normalWS;
                output.color = lerp(_BaseColor, _AccentColor, saturate(localPosition.z * 1.35 + 0.5));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 lightDirection = normalize(float3(0.25, 0.85, 0.35));
                float ndotl = saturate(dot(normalize(input.normalWS), lightDirection));
                float lighting = lerp(0.42, 1.0, ndotl);
                return half4(input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
