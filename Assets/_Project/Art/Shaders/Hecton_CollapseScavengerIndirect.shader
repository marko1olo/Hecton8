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
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float FastTriangleSine(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
            }

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

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
                float4x4 instanceMatrix = _HectonScavengerMatrices[instanceID];
                float3 anchorWS = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float phase = frac((anchorWS.x + anchorWS.z) * 0.17389);
                float wobble = FastTriangleSine(_Time.y * 7.0 + phase * 6.2831853) * 0.025;
                float3 localPosition = input.positionOS.xyz;
                localPosition.y += wobble * saturate(localPosition.z + 0.35);

                float4 positionWS = mul(instanceMatrix, float4(localPosition, 1.0));
                float3 normalWS = SafeNormalize3(mul((float3x3)instanceMatrix, input.normalOS), float3(0.0, 1.0, 0.0));

                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.normalWS = normalWS;
                output.color = lerp(_BaseColor, _AccentColor, saturate(localPosition.z * 1.35 + 0.5));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 lightDirection = float3(0.2660, 0.9045, 0.3724);
                float3 normalWS = SafeNormalize3(input.normalWS, float3(0.0, 1.0, 0.0));
                float ndotl = saturate(dot(normalWS, lightDirection));
                float lighting = lerp(0.42, 1.0, ndotl);
                return half4(input.color.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
