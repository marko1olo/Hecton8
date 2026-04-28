Shader "Hecton8/World/WreckIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Mask Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.42
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        StructuredBuffer<float4x4> _HectonWreckMatrices;
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _Cutoff;
            float _DepthBias;
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
            float3 viewDirWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fogFactor : TEXCOORD4;
        };

        float4x4 ResolveWreckMatrix(uint instanceID)
        {
            return _HectonWreckMatrices[instanceID];
        }

        float3 SafeNormalize3(float3 value)
        {
            float lenSq = dot(value, value);
            return lenSq > 0.0001 ? value * rsqrt(lenSq) : float3(0.0, 1.0, 0.0);
        }

        float3 TransformWreckNormal(float4x4 instanceMatrix, float3 normalOS)
        {
            return SafeNormalize3(mul((float3x3)instanceMatrix, normalOS));
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float4x4 instanceMatrix = ResolveWreckMatrix(input.instanceID);
            float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));
            output.positionWS = positionWS.xyz;
            output.normalWS = TransformWreckNormal(instanceMatrix, input.normalOS);
            output.positionCS = TransformWorldToHClip(positionWS.xyz);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, _DepthBias, 1.0);
            output.viewDirWS = SafeNormalize3(GetWorldSpaceViewDir(positionWS.xyz));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleWreckSurface(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        half4 SamplePackedMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
        }

        half3 EvaluateWreckLighting(
            float3 positionWS,
            half3 normalWS,
            half3 viewDirWS,
            half3 albedo,
            half metallic,
            half smoothness,
            half ambientOcclusion)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = SampleSH(normalWS) * albedo * ambientOcclusion * caveAmbientFactor;
            half specularStrength = lerp(0.04h, 0.22h, metallic);
            half specularPower = lerp(16.0h, 96.0h, smoothness);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = SafeNormalize3(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = SafeNormalize3(lightDir + viewDirWS);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadow(positionWS, normalWS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);

            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 surface = SampleWreckSurface(input.uv);
        #if defined(_ALPHATEST_ON)
            clip(surface.a - _Cutoff);
        #endif

            half4 packedMask = SamplePackedMask(input.uv);
            half metallic = saturate(packedMask.r * _Metallic);
            half ambientOcclusion = saturate(lerp(1.0h, packedMask.g, _OcclusionStrength));
            half smoothness = saturate(packedMask.b * _Smoothness);
            half emissionMask = saturate(packedMask.a);

            half3 normalWS = SafeNormalize3(input.normalWS);
            half3 viewDirWS = SafeNormalize3(input.viewDirWS);
            half3 litColor = EvaluateWreckLighting(
                input.positionWS,
                normalWS,
                viewDirWS,
                surface.rgb,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 emission = _EmissionColor.rgb * emissionMask;
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, input.fogFactor);
            return half4(finalColor, 1.0h);
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
