Shader "Hecton8/Fauna/LeviathanTentacleIndirect"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.28, 0.42, 0.44, 1)
        _TipColor("Tip Color", Color) = (0.10, 0.18, 0.20, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0.08, 0.65, 0.82, 1)
        [HDR] _SuctionGlowColor("Suction Glow Color", Color) = (0.04, 0.92, 0.78, 1)
        [HDR] _SssColor("SSS Color", Color) = (0.24, 0.72, 0.82, 1)
        _NormalScale("Normal Scale", Range(0, 2)) = 1.0
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.52
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EmissionStrength("Emission Strength", Range(0, 8)) = 0.65
        _SuctionGlowGain("Suction Glow Gain", Range(0, 24)) = 9.0
        _FlowSheenStrength("Flow Sheen Strength", Range(0, 2)) = 0.32
        _BaseRadiusReference("Base Radius Reference", Range(0.001, 1)) = 0.22
        _TipRadiusReference("Tip Radius Reference", Range(0.001, 1)) = 0.055
        _SssDistortion("SSS Distortion", Range(0, 2)) = 0.42
        _SssPower("SSS Power", Range(0.1, 16)) = 3.4
        _SssScale("SSS Scale", Range(0, 4)) = 0.92
        _H8LeviathanTentacleFxTier("FX Tier", Range(0, 1)) = 1.0
        _DepthBias("Depth Bias", Range(0, 0.01)) = 0.0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.02
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
        ZTest LEqual

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma multi_compile _ DOTS_INSTANCING_ON

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

        #define HECTON_LEVIATHAN_TENTACLE_SEGMENTS 20.0
        #define HECTON_LEVIATHAN_TENTACLE_LAST_SEGMENT 19.0

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        StructuredBuffer<float4x4> _H8LeviathanTentacleMatrices;
        StructuredBuffer<float> _H8LeviathanTentacleRadius;
        StructuredBuffer<float4> _H8AbyssalFlowField;

        CBUFFER_START(_H8LeviathanTentacleGlobals)
            float4 _H8LeviathanTentacleRadiusFxFlow;
            float4 _H8AbyssalFlowResolution;
            float4 _H8AbyssalFlowCenter;
            float4 _H8AbyssalFlowSpacing;
        CBUFFER_END

        #define _BaseRadiusReference _H8LeviathanTentacleRadiusFxFlow.x
        #define _TipRadiusReference _H8LeviathanTentacleRadiusFxFlow.y
        #define _H8LeviathanTentacleFxTier _H8LeviathanTentacleRadiusFxFlow.z
        #define _H8AbyssalFlowActive _H8LeviathanTentacleRadiusFxFlow.w

        float4x4 _GlobalBiolumDearLieGroups;
        float4 _GlobalBiolumParams;
        float4 _GlobalBiolumClock;
        float4 _GlobalBiolumAupOffset;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _TipColor;
            float4 _EmissionColor;
            float4 _SuctionGlowColor;
            float4 _SssColor;
            float _NormalScale;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EmissionStrength;
            float _SuctionGlowGain;
            float _FlowSheenStrength;
            float _SssDistortion;
            float _SssPower;
            float _SssScale;
            float _DepthBias;
            float _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            half fogFactor : TEXCOORD5;
            half segment01 : TEXCOORD6;
            half radius : TEXCOORD7;
            half4 flowWS : TEXCOORD8;
            half4 vertexColor : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float4x4 ResolveTentacleMatrix(uint instanceID)
        {
            return _H8LeviathanTentacleMatrices[instanceID];
        }

        half ResolveTentacleRadius(uint instanceID)
        {
            return (half)max(_H8LeviathanTentacleRadius[instanceID], 0.001);
        }

        half ResolveSegment01(uint instanceID)
        {
            uint segmentIndex = instanceID - (instanceID / 20u) * 20u;
            return (half)((float)segmentIndex / HECTON_LEVIATHAN_TENTACLE_LAST_SEGMENT);
        }

        half CheapTriangle01(float phase)
        {
            return (half)(1.0 - abs(frac(phase) * 2.0 - 1.0));
        }

        half3 ResolveAbyssalFlowDirection(float3 positionWS)
        {
            if (_H8AbyssalFlowActive < 0.5)
                return half3(0.0h, 0.0h, 0.0h);

            int3 resolution = max((int3)_H8AbyssalFlowResolution.xyz, int3(1, 1, 1));
            int publishedCount = max((int)_H8AbyssalFlowResolution.w, 0);
            float horizontalSpacing = max(abs(_H8AbyssalFlowSpacing.x), 0.001);
            float verticalSpacing = max(abs(_H8AbyssalFlowSpacing.y), 0.001);
            float invHorizontalSpacing = rcp(max(horizontalSpacing, 0.001));
            float invVerticalSpacing = rcp(max(verticalSpacing, 0.001));
            int3 halfExtent = int3(resolution.x >> 1, resolution.y >> 1, resolution.z >> 1);
            float3 local = positionWS - _H8AbyssalFlowCenter.xyz;
            int3 cell = int3(
                (int)round(local.x * invHorizontalSpacing) + halfExtent.x,
                (int)round(local.y * invVerticalSpacing) + halfExtent.y,
                (int)round(local.z * invHorizontalSpacing) + halfExtent.z);
            if (any(cell < int3(0, 0, 0)) || any(cell >= resolution) || publishedCount <= 0)
                return half3(0.0h, 0.0h, 0.0h);

            uint index = (uint)(cell.x + (cell.z * resolution.x) + (cell.y * resolution.x * resolution.z));
            if (index >= (uint)publishedCount)
                return half3(0.0h, 0.0h, 0.0h);

            float3 flow = _H8AbyssalFlowField[index].xyz;
            float flowSq = dot(flow, flow);
            if (!isfinite(flowSq) || flowSq <= 0.0001)
                return half3(0.0h, 0.0h, 0.0h);

            return (half3)HectonCoreLitSafeNormalize(flow);
        }

        float3 TransformTentaclePosition(float4x4 instanceMatrix, float3 positionOS)
        {
            return mul(instanceMatrix, float4(HectonCoreLitSanitizePositionOS(positionOS), 1.0)).xyz;
        }

        float3 TransformTentacleVector(float4x4 instanceMatrix, float3 vectorOS)
        {
            return HectonCoreLitSafeNormalize(mul((float3x3)instanceMatrix, vectorOS));
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            uint instanceID = input.instanceID;
        #if UNITY_ANY_INSTANCING_ENABLED
            instanceID = unity_InstanceID;
        #endif

            float4x4 instanceMatrix = ResolveTentacleMatrix(instanceID);
            output.positionWS = HectonCoreLitSanitizePositionWS(TransformTentaclePosition(instanceMatrix, input.positionOS.xyz));
            output.normalWS = TransformTentacleVector(instanceMatrix, input.normalOS);
            output.tangentWS = float4(TransformTentacleVector(instanceMatrix, input.tangentOS.xyz), input.tangentOS.w);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(output.positionCS, _DepthBias, 1.0);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            output.segment01 = ResolveSegment01(instanceID);
            output.radius = ResolveTentacleRadius(instanceID);
            half3 flowDirection = ResolveAbyssalFlowDirection(output.positionWS);
            output.flowWS = half4(flowDirection, step(0.0001h, dot(flowDirection, flowDirection)));
            output.vertexColor = saturate(input.color);
            return output;
        }

        half3 ResolveTentacleNormal(Varyings input)
        {
            half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
            float3 baseNormal = HectonCoreLitSafeNormalize(input.normalWS);
            float3 tangentWS = HectonCoreLitSafeNormalize(input.tangentWS.xyz);
            float3 bitangentWS = HectonCoreLitSafeNormalize(cross(baseNormal, tangentWS) * input.tangentWS.w);
            float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, baseNormal);
            return (half3)HectonCoreLitSafeNormalize(TransformTangentToWorld(tangentNormal, tangentToWorld));
        }

        half3 EvaluateTentacleLighting(
            float3 positionWS,
            float4 positionCS,
            half3 normalWS,
            half3 viewDirWS,
            half3 albedo,
            half metallic,
            half smoothness,
            half ambientOcclusion)
        {
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
            half3 color = H8CustomLightProbeResolveAmbient(positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * albedo * ambientOcclusion * caveAmbientFactor;
            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = (half3)HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specularStrength = lerp(0.05h, 0.22h, metallic);
            half specularEnergy = smoothness * specularStrength;
            half3 halfDir = (half3)HectonCoreLitSafeNormalize(lightDir + viewDirWS);
            half specularBase = saturate(dot(normalWS, halfDir));
            half specular2 = specularBase * specularBase;
            half specular4 = specular2 * specular2;
            half specular8 = specular4 * specular4;
            half specular16 = specular8 * specular8;
            half specular = specular16 * specularEnergy * step(0.0001h, nDotL * specularEnergy);
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow * contactShadow);
            return color;
        }

        half4 ResolveTentacleGlobalBiolum(float3 positionWS)
        {
            int activeCount = min(max((int)_GlobalBiolumParams.x, 0), 4);
            if (activeCount <= 0)
                return half4(0.0h, 0.0h, 0.0h, 0.0h);

            float selector = frac(abs(positionWS.x * 0.031 + positionWS.z * 0.067 + _GlobalBiolumAupOffset.x * 0.0019 + _GlobalBiolumAupOffset.z * 0.0012));
            int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
            float4 state = _GlobalBiolumDearLieGroups[stateIndex];
            half strobe = saturate((half)_GlobalBiolumParams.z);
            half qualityCurve = saturate((half)_GlobalBiolumParams.y);
            qualityCurve = qualityCurve * qualityCurve * (3.0h - 2.0h * qualityCurve);
            int secondaryIndex = stateIndex + 1;
            if (secondaryIndex >= activeCount)
                secondaryIndex = 0;
            float4 secondaryState = _GlobalBiolumDearLieGroups[secondaryIndex];
            half overdrive = 0.0h;
            half godSpark = 0.0h;
            half godHaze = 0.0h;
            half overPulse = (half)(1.0 - abs(frac(_GlobalBiolumClock.x * 0.07 + selector * 3.0) * 2.0 - 1.0));
            half filament = (half)(1.0 - abs(frac(positionWS.x * 0.119 + positionWS.y * 0.137 + positionWS.z * 0.101 + _GlobalBiolumClock.x * 0.18) * 2.0 - 1.0));
            godHaze = smoothstep(0.48h, 0.94h, overPulse) * (0.48h + filament * 0.52h) * qualityCurve;
            godSpark = smoothstep(0.84h, 0.99h, filament) * overPulse * qualityCurve;
            overdrive = saturate(overPulse * 0.35h + godSpark * 0.18h) * qualityCurve;
            half3 color = lerp((half3)state.rgb, half3(1.0h, 1.0h, 1.0h), strobe);
            half intensity = clamp(max((half)state.w, strobe * 10.0h), 0.0h, 10.0h);
            color = lerp(color, (half3)secondaryState.rgb, overdrive);
            color = saturate(color + godHaze * half3(0.03h, 0.13h, 0.18h));
            intensity = clamp(intensity + (half)secondaryState.w * overdrive + godSpark * 0.45h + godHaze * 0.22h, 0.0h, 10.0h);
            return half4(color, intensity);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(surface.a - (half)_Cutoff);

            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half fxTier = saturate((half)_H8LeviathanTentacleFxTier);
            half3 normalWS = (half3)HectonCoreLitSafeNormalize(input.normalWS);
            [branch]
            if (fxTier > 0.5h)
                normalWS = ResolveTentacleNormal(input);
            half3 viewDirWS = (half3)HectonCoreLitSafeNormalize(input.viewDirWS);
            half segment01 = saturate(input.segment01);
            half middleMask = saturate(1.0h - abs(segment01 * 2.0h - 1.0h));
            half3 albedo = surface.rgb * lerp((half3)_BaseColor.rgb, (half3)_TipColor.rgb, segment01);
            half metallic = decodedMask.metallic;
            half smoothness = decodedMask.smoothness;
            half ambientOcclusion = decodedMask.occlusion;
            half emissionMask = decodedMask.emissionMask;

            HectonCoreLitApplySedimentOverlay(input.positionWS, normalWS, albedo, metallic, smoothness);

            half referenceRadius = (half)lerp(_BaseRadiusReference, _TipRadiusReference, segment01);
            half radiusPulse = saturate((input.radius - referenceRadius) * (half)_SuctionGlowGain) * middleMask;
            half flowActive = saturate((half)_H8AbyssalFlowActive);
            half flowSheen = 0.0h;
            [branch]
            if (fxTier > 0.5h)
            {
                half flowPulse = CheapTriangle01(dot(input.positionWS.xz, float2(0.037, 0.061)) + _Time.y * 0.41 + segment01 * 2.37);
                half3 flowDirection = input.flowWS.xyz;
                half flowCellActive = saturate(input.flowWS.w);
                half flowAlignment = saturate(dot(flowDirection, normalWS) * 0.5h + 0.5h);
                flowSheen = flowActive * flowCellActive * flowPulse * lerp(0.35h, 1.0h, flowAlignment) * (half)_FlowSheenStrength;
            }
            half rim = saturate(1.0h - dot(normalWS, viewDirWS));
            half rim2 = rim * rim;

            half3 litColor = EvaluateTentacleLighting(
                input.positionWS,
                input.positionCS,
                normalWS,
                viewDirWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 sss = half3(0.0h, 0.0h, 0.0h);
            half3 caustics = half3(0.0h, 0.0h, 0.0h);
            half3 biolum = half3(0.0h, 0.0h, 0.0h);
            [branch]
            if (fxTier > 0.5h)
            {
                sss = HectonCoreLitEvaluateOrganicSss(
                    viewDirWS,
                    (half3)HectonCoreLitSafeNormalize(_MainLightPosition.xyz),
                    normalWS,
                    _SssColor.rgb,
                    _SssDistortion,
                    _SssPower,
                    _SssScale);
                caustics = HectonCoreLitEvaluateProjectedCausticsScattering(input.positionWS, normalWS) * albedo;
                biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask;
            }
            half4 globalBiolumState = ResolveTentacleGlobalBiolum(input.positionWS);
            half globalBiolumMask = step(0.001h, globalBiolumState.w);
            biolum += globalBiolumState.rgb * (globalBiolumState.w * 0.08h * emissionMask * globalBiolumMask);
            half3 emission = _EmissionColor.rgb * (_EmissionStrength * emissionMask);
            emission += _SuctionGlowColor.rgb * (_SuctionGlowColor.a * radiusPulse * (0.55h + rim2));
            emission += _EmissionColor.rgb * (flowSheen * (0.18h + rim2 * 0.82h));
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + caustics + sss + biolum + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetTentacleShadowPositionHClip(Attributes input, uint instanceID)
        {
            float4x4 instanceMatrix = ResolveTentacleMatrix(instanceID);
            float3 positionWS = TransformTentaclePosition(instanceMatrix, input.positionOS.xyz);
            float3 normalWS = TransformTentacleVector(instanceMatrix, input.normalOS);
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
        #if UNITY_REVERSED_Z
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            return positionCS;
        }

        float4 GetTentacleDepthPositionHClip(Attributes input, uint instanceID)
        {
            float4x4 instanceMatrix = ResolveTentacleMatrix(instanceID);
            float3 positionWS = TransformTentaclePosition(instanceMatrix, input.positionOS.xyz);
            return TransformWorldToHClip(positionWS);
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                output.positionCS = GetTentacleShadowPositionHClip(input, instanceID);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - (half)_Cutoff);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(Attributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                output.positionCS = GetTentacleDepthPositionHClip(input, instanceID);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - (half)_Cutoff);
                return 0.0h;
            }
            ENDHLSL
        }
    }
}
