Shader "Hecton8/World/ScatterIndirectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        [NoScaleOffset] _HectonMicroNormalTex("Micro Normal 128", 2D) = "bump" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.35
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EnvironmentalWear("Environmental Wear", Range(0, 1)) = 0.0
        _RustSaltColor("Rust/Salt Wear Color", Color) = (0.62, 0.35, 0.16, 1)
        _MicroNormalStrength("Micro Normal Strength", Range(0, 1)) = 0.24
        _MicroNormalTiling("Micro Normal Tiling", Range(4, 128)) = 52
        _StochasticTilingStrength("Stochastic Tiling Strength", Range(0, 1)) = 0.55
        _StormRainDripAmplitude("Storm Rain Drip Amplitude", Range(0, 0.025)) = 0.003
        _StormRainDripTiling("Storm Rain Drip Tiling", Range(0.5, 16)) = 5
        _StormRainDripSpeed("Storm Rain Drip Speed", Range(0, 8)) = 1.8
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
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
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
            float4 _RustSaltColor;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EnvironmentalWear;
            float _MicroNormalStrength;
            float _MicroNormalTiling;
            float _StochasticTilingStrength;
            float _StormRainDripAmplitude;
            float _StormRainDripTiling;
            float _StormRainDripSpeed;
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
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ScatterInstanceGpuData ResolveScatterInstance(uint instanceID)
        {
            return _HectonScatterInstances[_HectonVisibleScatterIndices[instanceID]];
        }

        float2 ResolveScatterYawOctant(float rotation)
        {
            uint sector = (uint)rotation & 7u;
            if (sector == 0u) return float2(1.0, 0.0);
            if (sector == 1u) return float2(0.70710677, 0.70710677);
            if (sector == 2u) return float2(0.0, 1.0);
            if (sector == 3u) return float2(-0.70710677, 0.70710677);
            if (sector == 4u) return float2(-1.0, 0.0);
            if (sector == 5u) return float2(-0.70710677, -0.70710677);
            if (sector == 6u) return float2(0.0, -1.0);
            return float2(0.70710677, -0.70710677);
        }

        void BuildScatterBasis(float3 normalWS, float rotation, float scale, out float3 rightWS, out float3 upWS, out float3 forwardWS)
        {
            float2 forwardXZ = ResolveScatterYawOctant(rotation);
            upWS = normalWS.y < 0.0 ? float3(0.0, -1.0, 0.0) : float3(0.0, 1.0, 0.0);
            rightWS = float3(forwardXZ.y, 0.0, -forwardXZ.x) * scale;
            forwardWS = float3(forwardXZ.x, 0.0, forwardXZ.y) * scale;
            upWS *= scale;
        }

        float3 ResolveScatterNormal(float3 normalOS, float3 rightWS, float3 upWS, float3 forwardWS, float invScale)
        {
            float3 rightAxisWS = rightWS * invScale;
            float3 upAxisWS = upWS * invScale;
            float3 forwardAxisWS = forwardWS * invScale;
            return rightAxisWS * normalOS.x + upAxisWS * normalOS.y + forwardAxisWS * normalOS.z;
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
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = max(instanceData.PositionScale.w, 0.05);
            float invScale = rcp(scale);
            float3 normalWS = instanceData.NormalRotation.xyz;
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);

            float3 localPosition = input.positionOS.xyz;
            float3 resolvedPositionWS = HectonCoreLitSanitizePositionWS(
                positionWS + rightWS * localPosition.x + upWS * localPosition.y + forwardWS * localPosition.z);
            float3 resolvedNormalWS = ResolveScatterNormal(input.normalOS, rightWS, upWS, forwardWS, invScale);

            output.positionWS = HectonCoreLitApplyStormRainDripVertexRipple(resolvedPositionWS, resolvedNormalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);
            output.normalWS = resolvedNormalWS;
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, _DepthBias, 1.0);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 SampleSurface(float2 uv)
        {
            return HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), uv, uv * 0.031, (half)_StochasticTilingStrength) * _BaseColor;
        }

        half4 SamplePackedMask(float2 uv)
        {
            return HectonCoreLitSampleStochastic2D(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), uv, uv * 0.031, (half)_StochasticTilingStrength);
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

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (nDotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                if (specularBase > 0.0001h)
                {
                    half specular2 = specularBase * specularBase;
                    half specular4 = specular2 * specular2;
                    half specular8 = specular4 * specular4;
                    half specular16 = specular8 * specular8;
                    half specular32 = specular16 * specular16;
                    half specular64 = specular32 * specular32;
                    specular = lerp(specular16, specular64, smoothness) * specularEnergy;
                }
            }
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);
            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 surface = SampleSurface(input.uv);
            half4 packedMask = SamplePackedMask(input.uv);
            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;
            half3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
            normalWS = HectonCoreLitApplyTripleDetailMicroNormals(input.positionWS, normalWS, (half)_MicroNormalStrength, (half)_MicroNormalTiling, 2.0h);
            half3 viewDirWS = HectonCoreLitSafeNormalize(input.viewDirWS);
            half3 albedo = surface.rgb;
            HectonCoreLitApplyEnvironmentalWear(input.positionWS, normalWS, (half)_EnvironmentalWear, (half3)_RustSaltColor.rgb, albedo, metallic, smoothness);

            half3 litColor = EvaluateLighting(
                input.positionWS,
                normalWS,
                viewDirWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask * 0.2h;
            half3 emission = _EmissionColor.rgb * emissionMask + biolum;
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input, uint instanceID)
        {
            ScatterInstanceGpuData instanceData = ResolveScatterInstance(instanceID);
            float3 positionWS = instanceData.PositionScale.xyz;
            float scale = max(instanceData.PositionScale.w, 0.05);
            float invScale = rcp(scale);
            float3 normalWS = instanceData.NormalRotation.xyz;
            float rotation = instanceData.NormalRotation.w;

            float3 rightWS;
            float3 upWS;
            float3 forwardWS;
            BuildScatterBasis(normalWS, rotation, scale, rightWS, upWS, forwardWS);
            float3 resolvedPositionWS = positionWS + rightWS * input.positionOS.x + upWS * input.positionOS.y + forwardWS * input.positionOS.z;
            float3 resolvedNormalWS = ResolveScatterNormal(input.normalOS, rightWS, upWS, forwardWS, invScale);
            resolvedPositionWS = HectonCoreLitApplyStormRainDripVertexRipple(resolvedPositionWS, resolvedNormalWS, (half)_StormRainDripAmplitude, (half)_StormRainDripTiling, (half)_StormRainDripSpeed);

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
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                output.positionCS = GetShadowPositionHClip(input, instanceID);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0.0h;
            }
            ENDHLSL
        }
    }
}
