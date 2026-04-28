Shader "Hecton8/World/WreckIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        StructuredBuffer<float4x4> _HectonWreckMatrices;
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };

        float4x4 ResolveWreckMatrix(uint instanceID)
        {
            return _HectonWreckMatrices[instanceID];
        }

        float3 TransformWreckNormal(float4x4 instanceMatrix, float3 normalOS)
        {
            return normalize(mul((float3x3)instanceMatrix, normalOS));
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float4x4 instanceMatrix = ResolveWreckMatrix(input.instanceID);
            float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));
            output.positionWS = positionWS.xyz;
            output.normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            output.positionCS = TransformWorldToHClip(positionWS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        half4 SampleWreckSurface(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 surface = SampleWreckSurface(input.uv);
        #if defined(_ALPHATEST_ON)
            clip(surface.a - _Cutoff);
        #endif

            float3 normalWS = normalize(input.normalWS);
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half ndotl = saturate(dot(normalWS, mainLight.direction));
            half3 diffuse = surface.rgb * (SampleSH(normalWS) + (mainLight.color * (ndotl * mainLight.shadowAttenuation)));
            diffuse += _EmissionColor.rgb;
            return half4(diffuse, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            float4x4 instanceMatrix = ResolveWreckMatrix(input.instanceID);
            float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));
            float3 normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS.xyz, normalWS, lightDirectionWS));
        #if UNITY_REVERSED_Z
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                half alpha = SampleWreckSurface(input.uv).a;
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
                return 0.0h;
            }
            ENDHLSL
        }
    }
}
