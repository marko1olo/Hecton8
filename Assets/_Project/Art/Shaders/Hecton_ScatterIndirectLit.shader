Shader "Hecton8/World/ScatterIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Mask Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.35
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        struct ScatterInstanceGpuData
        {
            float4 PositionScale;
            float4 NormalRotation;
        };

        StructuredBuffer<ScatterInstanceGpuData> _HectonScatterInstances;
        StructuredBuffer<uint> _HectonVisibleScatterIndices;
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

        ScatterInstanceGpuData ResolveScatterInstance(uint instanceID)
        {
            return _HectonScatterInstances[_HectonVisibleScatterIndices[instanceID]];
        }

        void BuildScatterBasis(float3 normalWS, float rotation, float scale, out float3 rightWS, out float3 upWS, out float3 forwardWS)
        {
            upWS = HectonCoreLitSafeNormalize(normalWS);
            float3 anchorRight = abs(upWS.y) > 0.99 ? float3(1.0, 0.0, 0.0) : normalize(cross(float3(0.0, 1.0, 0.0), upWS));
            float3 anchorForward = normalize(cross(upWS, anchorRight));
            float sinAngle;
            float cosAngle;
            sincos(rotation, sinAngle, cosAngle);
            rightWS = (anchorRight * cosAngle + anchorForward * sinAngle) * scale;
            forwardWS = normalize(cross(rightWS, upWS)) * scale;
            upWS *= scale;
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(input.instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = max(instanceData.PositionScale.w, 0.05);
            float3 normalWS = normalize(instanceData.NormalRotation.xyz);
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);

            float3 localPosition = input.positionOS.xyz;
            float3 resolvedPositionWS = positionWS + rightWS * localPosition.x + upWS * localPosition.y + forwardWS * localPosition.z;
            float3 resolvedNormalWS = normalize(
                normalize(rightWS) * input.normalOS.x +
                normalize(upWS) * input.normalOS.y +
                normalize(forwardWS) * input.normalOS.z);

            output.positionWS = resolvedPositionWS;
            output.normalWS = resolvedNormalWS;
            output.positionCS = TransformWorldToHClip(resolvedPositionWS);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(resolvedPositionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleSurface(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        half4 SamplePackedMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
        }

        half3 EvaluateLighting(
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
            half specularStrength = lerp(0.04h, 0.18h, metallic);
            half specularPower = lerp(14.0h, 72.0h, smoothness);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = HectonCoreLitSafeNormalize(lightDir + viewDirWS);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadow(positionWS, normalWS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);
            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 surface = SampleSurface(input.uv);
            half4 packedMask = SamplePackedMask(input.uv);
            half metallic = saturate(packedMask.r * _Metallic);
            half ambientOcclusion = saturate(lerp(1.0h, packedMask.g, _OcclusionStrength));
            half smoothness = saturate(packedMask.b * _Smoothness);
            half emissionMask = saturate(packedMask.a);
            half3 normalWS = normalize(input.normalWS);
            half3 viewDirWS = normalize(input.viewDirWS);

            half3 litColor = EvaluateLighting(
                input.positionWS,
                normalWS,
                viewDirWS,
                surface.rgb,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask * 0.2h;
            half3 emission = _EmissionColor.rgb * emissionMask + biolum;
            half3 finalColor = MixFog(litColor + emission, input.fogFactor);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(input.instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = max(instanceData.PositionScale.w, 0.05);
            float3 normalWS = normalize(instanceData.NormalRotation.xyz);
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);
            float3 resolvedPositionWS = positionWS + rightWS * input.positionOS.x + upWS * input.positionOS.y + forwardWS * input.positionOS.z;
            float3 resolvedNormalWS = normalize(
                normalize(rightWS) * input.normalOS.x +
                normalize(upWS) * input.normalOS.y +
                normalize(forwardWS) * input.normalOS.z);

            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(resolvedPositionWS, resolvedNormalWS, lightDirectionWS));
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
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0.0h;
            }
            ENDHLSL
        }
    }
}
