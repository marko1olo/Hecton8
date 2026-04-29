Shader "Hecton8/Fauna/LeviathanOrganic"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MaskMap("Mask Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        [HDR] _SssColor("SSS Color", Color) = (0.44, 0.86, 0.92, 1)
        [HDR] _WoundColor("Wound Color", Color) = (0.42, 0.05, 0.04, 1)
        [HDR] _WoundBurnColor("Wound Burn Color", Color) = (0.10, 0.01, 0.01, 1)
        _NormalScale("Normal Scale", Range(0, 2)) = 1.0
        _Metallic("Metallic Scale", Range(0, 1)) = 0.0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.45
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0
        _EmissionStrength("Emission Strength", Range(0, 8)) = 1.0
        _SssDistortion("SSS Distortion", Range(0, 2)) = 0.48
        _SssPower("SSS Power", Range(0.1, 16)) = 3.8
        _SssScale("SSS Scale", Range(0, 4)) = 1.15
        _WetnessStrength("Wetness Strength", Range(0, 2)) = 1.0
        _WetnessSmoothnessBoost("Wetness Smoothness Boost", Range(0, 1)) = 0.28
        _WetnessNormalWobble("Wetness Normal Wobble", Range(0, 1)) = 0.08
        _WetnessVelocityWS("Wetness Velocity WS", Vector) = (0, 0, 0, 0)
        _WoundSmoothnessDrop("Wound Smoothness Drop", Range(0, 1)) = 0.34
        _WoundEmissionBoost("Wound Emission Boost", Range(0, 4)) = 0.55
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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _SssColor;
            float4 _WoundColor;
            float4 _WoundBurnColor;
            float4 _WetnessVelocityWS;
            float _NormalScale;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _EmissionStrength;
            float _SssDistortion;
            float _SssPower;
            float _SssScale;
            float _WetnessStrength;
            float _WetnessSmoothnessBoost;
            float _WetnessNormalWobble;
            float _WoundSmoothnessDrop;
            float _WoundEmissionBoost;
            float _DepthBias;
        CBUFFER_END

        float _HectonCreatureWoundCount;
        float4 _HectonCreatureWounds[8];
        float4x4 _HectonCreatureWoundOwnerWorldToLocal;
        float4 _HectonCreatureWoundOwnerSphere;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 positionOS : TEXCOORD6;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            half fogFactor : TEXCOORD5;
        };

        float3x3 BuildTangentToWorld(float3 normalWS, float4 tangentWS)
        {
            float3 tangent = normalize(tangentWS.xyz);
            float3 bitangent = normalize(cross(normalWS, tangent) * tangentWS.w);
            return float3x3(tangent, bitangent, normalWS);
        }

        float3 ApplyWetnessNormalWobble(float3 normalWS, float3 positionWS)
        {
            float3 velocityWS = _WetnessVelocityWS.xyz;
            float velocityMagnitude = length(velocityWS);
            if (velocityMagnitude <= 0.001 || _WetnessNormalWobble <= 0.0001 || _WetnessStrength <= 0.0001)
                return normalWS;

            float3 velocityDir = velocityWS / velocityMagnitude;
            float wobblePhase = _Time.y * (2.0 + velocityMagnitude * 0.2) + dot(positionWS, velocityDir) * 0.12;
            float3 wobbleAxis = normalize(cross(normalWS, velocityDir + float3(0.0, 0.18, 0.0)));
            float wobbleStrength = saturate(velocityMagnitude * 0.05) * _WetnessNormalWobble * _WetnessStrength;
            return normalize(normalWS + wobbleAxis * (sin(wobblePhase) * wobbleStrength));
        }

        half2 EvaluateWoundMask(float3 positionWS)
        {
            if (_HectonCreatureWoundCount < 0.5)
                return 0.0h.xx;

            float3 toOwner = positionWS - _HectonCreatureWoundOwnerSphere.xyz;
            float ownerRadius = max(_HectonCreatureWoundOwnerSphere.w, 0.001);
            if (dot(toOwner, toOwner) > ownerRadius * ownerRadius)
                return 0.0h.xx;

            float3 ownerLocalPosition = mul(_HectonCreatureWoundOwnerWorldToLocal, float4(positionWS, 1.0)).xyz;
            half woundMask = 0.0h;
            half burnMask = 0.0h;
            [unroll]
            for (int woundIndex = 0; woundIndex < 8; woundIndex++)
            {
                if ((float)woundIndex >= _HectonCreatureWoundCount)
                    break;

                float4 wound = _HectonCreatureWounds[woundIndex];
                float woundRadius = max(wound.w, 0.001);
                float woundDistance = distance(ownerLocalPosition, wound.xyz);
                half woundContribution = saturate(1.0h - (half)(woundDistance / woundRadius));
                half coreContribution = saturate(1.0h - (half)(woundDistance / max(woundRadius * 0.45, 0.001)));
                woundMask = max(woundMask, woundContribution * woundContribution);
                burnMask = max(burnMask, coreContribution * coreContribution);
            }

            return half2(woundMask, burnMask);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionWS = positionInputs.positionWS;
            output.positionOS = input.positionOS.xyz;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
            output.positionCS = HectonCoreLitApplyClipSpaceDepthBias(positionInputs.positionCS, _DepthBias, 1.0);
            output.viewDirWS = HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
            half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalScale);
            float3x3 tangentToWorld = BuildTangentToWorld(normalize(input.normalWS), input.tangentWS);
            half3 normalWS = normalize(TransformTangentToWorld(tangentNormal, tangentToWorld));
            normalWS = ApplyWetnessNormalWobble(normalWS, input.positionWS);

            half metallic = saturate(packedMask.r * _Metallic);
            half ambientOcclusion = saturate(lerp(1.0h, packedMask.g, _OcclusionStrength));
            half smoothness = saturate(packedMask.b * _Smoothness);
            half emissionMask = saturate(packedMask.a);
            half3 viewDirWS = normalize(input.viewDirWS);
            half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(input.positionWS, normalWS);
            half2 woundMasks = EvaluateWoundMask(input.positionWS);
            half woundMask = woundMasks.x;
            half woundBurnMask = woundMasks.y;

            float wetnessSignal = saturate(length(_WetnessVelocityWS.xyz) * 0.05) * _WetnessStrength;
            smoothness = saturate(smoothness + wetnessSignal * _WetnessSmoothnessBoost);
            smoothness = saturate(smoothness * (1.0h - woundMask * _WoundSmoothnessDrop));
            half3 woundColor = lerp(_WoundColor.rgb, _WoundBurnColor.rgb, woundBurnMask);
            surface.rgb = lerp(surface.rgb, woundColor, woundMask);

            half3 color = SampleSH(normalWS) * surface.rgb * ambientOcclusion * caveAmbientFactor;
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half3 halfDir = HectonCoreLitSafeNormalize(lightDir + viewDirWS);
            half specularStrength = lerp(0.05h, 0.22h, metallic);
            half specularPower = lerp(24.0h, 112.0h, smoothness);
            half specular = pow(saturate(dot(normalWS, halfDir)), specularPower) * smoothness * specularStrength;
            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadow(input.positionWS, normalWS);
            color += (surface.rgb * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * contactShadow);

            half3 sss = HectonCoreLitEvaluateOrganicSss(
                viewDirWS,
                lightDir,
                normalWS,
                _SssColor.rgb,
                _SssDistortion,
                _SssPower,
                _SssScale);

            half3 caustics = HectonCoreLitEvaluateProjectedCausticsScattering(input.positionWS, normalWS) * surface.rgb;
            half3 biolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(input.positionWS) * emissionMask;
            half3 woundEmission = woundColor * (woundMask * _WoundEmissionBoost);
            half3 emission = (_EmissionColor.rgb * (_EmissionStrength * emissionMask)) + biolum + woundEmission;
            half3 finalColor = HectonCoreLitApplyNoirFog(color + caustics + emission + sss, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 GetShadowPositionHClip(Attributes input)
        {
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            float3 lightDirectionWS = _MainLightPosition.xyz;
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, normalInputs.normalWS, lightDirectionWS));
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
