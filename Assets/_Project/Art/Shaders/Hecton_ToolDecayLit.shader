Shader "Hecton8/Tools/DecayLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _MaskMap("Packed Mask (R Metallic G AO B Smoothness A Emission)", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _Metallic("Metallic Scale", Range(0, 1)) = 0
        _Smoothness("Smoothness Scale", Range(0, 1)) = 0.5
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _BumpScale("Normal Strength", Range(0, 2)) = 1
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
        #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _BumpScale;
            float _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            half3 viewDirWS : TEXCOORD3;
            float2 uv : TEXCOORD4;
            half fogFactor : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs positionInputs = GetVertexPositionInputs(HectonCoreLitSanitizePositionOS(input.positionOS.xyz));
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionWS = HectonCoreLitSanitizePositionWS(positionInputs.positionWS);
            output.positionCS = positionInputs.positionCS;
            output.normalWS = (half3)HectonCoreLitSafeNormalize(normalInputs.normalWS);
            output.tangentWS = half4((half3)HectonCoreLitSafeNormalize(normalInputs.tangentWS), input.tangentOS.w);
            output.viewDirWS = (half3)HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        half3 ResolveToolNormalWS(Varyings input, float2 uv)
        {
            float3 baseNormalWS = HectonCoreLitSafeNormalize(input.normalWS);
            float3 tangentWS = HectonCoreLitSafeNormalize(input.tangentWS.xyz);
            float3 bitangentWS = HectonCoreLitSafeNormalize(cross(baseNormalWS, tangentWS) * input.tangentWS.w);
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), (half)_BumpScale);
            float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, baseNormalWS);
            return (half3)HectonCoreLitSafeNormalize(TransformTangentToWorld(normalTS, tangentToWorld));
        }

        half3 EvaluateToolLighting(
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
            half specularStrength = lerp(0.04h, 0.18h, metallic);

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 lightDir = (half3)HectonCoreLitSafeNormalize(mainLight.direction);
            half nDotL = saturate(dot(normalWS, lightDir));
            half specular = 0.0h;
            half specularEnergy = smoothness * specularStrength;
            if (nDotL > 0.0001h && specularEnergy > 0.0001h)
            {
                half3 halfDir = (half3)HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                half specular2 = specularBase * specularBase;
                half specular4 = specular2 * specular2;
                half specular8 = specular4 * specular4;
                half specular16 = specular8 * specular8;
                half specular32 = specular16 * specular16;
                specular = lerp(specular16, specular32, smoothness) * specularEnergy;
            }

            half contactShadow = (half)HectonCoreLitEvaluateMainLightContactShadowFromDirection(positionWS, normalWS, mainLight.direction);
            half mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
            color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow * contactShadow);
            color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
            return color;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 rustPacked;
            half rustMask;
            float2 wearUv = HectonCoreLitResolveDynamicWearUv(
                input.uv,
                input.viewDirWS,
                input.normalWS,
                input.tangentWS.xyz,
                input.tangentWS.w,
                rustPacked,
                rustMask);

            half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, wearUv) * _BaseColor;
            clip(surface.a - _Cutoff);
            half4 packedMask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, wearUv);
            HectonPackedMaskV1 decodedMask = HectonCoreLitDecodePackedMaskV1(packedMask, (half)_Metallic, (half)_OcclusionStrength, (half)_Smoothness);
            half metallic = decodedMask.metallic;
            half ambientOcclusion = decodedMask.occlusion;
            half smoothness = decodedMask.smoothness;
            half emissionMask = decodedMask.emissionMask;
            half3 normalWS = ResolveToolNormalWS(input, wearUv);
            half3 albedo = surface.rgb;

            HectonCoreLitApplyDynamicWearPOM(
                wearUv,
                input.positionWS,
                input.viewDirWS,
                input.tangentWS.xyz,
                input.tangentWS.w,
                rustPacked,
                rustMask,
                albedo,
                normalWS,
                metallic,
                smoothness);

            half3 litColor = EvaluateToolLighting(
                input.positionWS,
                input.positionCS,
                normalWS,
                input.viewDirWS,
                albedo,
                metallic,
                smoothness,
                ambientOcclusion);
            half3 emission = _EmissionColor.rgb * emissionMask;
            half3 finalColor = HectonCoreLitApplyNoirFog(litColor + emission, input.fogFactor, input.positionWS);
            return half4(finalColor, 1.0h);
        }

        float4 ShadowVert(Attributes input) : SV_POSITION
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float3 positionWS = TransformObjectToWorld(HectonCoreLitSanitizePositionOS(input.positionOS.xyz));
            float3 normalWS = HectonCoreLitSafeNormalize(TransformObjectToWorldNormal(input.normalOS));
            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
#if UNITY_REVERSED_Z
            positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
            positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
            return positionCS;
        }

        half4 ShadowFrag() : SV_Target
        {
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            ENDHLSL
        }
    }

    FallBack Off
}
